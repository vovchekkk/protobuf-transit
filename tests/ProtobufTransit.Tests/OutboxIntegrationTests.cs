using System.Net.Sockets;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NUnit.Framework;
using ProtobufTransit.Tests.Messages;

namespace ProtobufTransit.Tests;

[TestFixture]
[Category("Integration")]
public class OutboxIntegrationTests
{
    private static string GetPostgresConnection() =>
        Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
        ?? "Host=127.0.0.1;Port=55439;Database=pbtransit_demo;Username=demo;Password=demo";

    private static string GetRabbitHost() =>
        Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "127.0.0.1";

    private static ushort GetRabbitPort()
    {
        var portStr = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
        return ushort.TryParse(portStr, out var port) ? port : (ushort)56789;
    }

    private static string GetRabbitUser() =>
        Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "demo";

    private static string GetRabbitPass() =>
        Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "demo";

    private IHost? _host;
    private static readonly TaskCompletionSource<ConsumeContext<SampleTextMessage>> ConsumerTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string _queueName = null!;
    private const string CustomCanonicalUrn = "urn:message:custom.wire.v1:sample-text-event";
    private const string CustomAlias = "CustomSampleTextAlias";

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var isIntegrationRequested = string.Equals(
            Environment.GetEnvironmentVariable("PROTOBUFTRANSIT_INTEGRATION"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        if (!isIntegrationRequested)
        {
            Assert.Ignore("Integration tests skipped by default. Set PROTOBUFTRANSIT_INTEGRATION=1 to run.");
            return;
        }

        var pgReachable = IsPostgresReachable();
        var rmqReachable = IsRabbitMqReachable();

        if (!pgReachable || !rmqReachable)
        {
            Assert.Fail($"PROTOBUFTRANSIT_INTEGRATION=1 is set, but required services are unreachable. PostgreSQL: {pgReachable}, RabbitMQ: {rmqReachable}");
            return;
        }

        _queueName = "pbtransit-outbox-verify-" + Guid.NewGuid().ToString("N");

        var registry = new ProtobufMessageRegistryBuilder()
            .Register(SampleTextMessage.Parser, CustomCanonicalUrn, CustomAlias)
            .Build();

        var builder = Host.CreateDefaultBuilder();

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<TestOutboxDbContext>(options =>
            {
                options.UseNpgsql(GetPostgresConnection());
            });

            services.AddMassTransit(x =>
            {
                x.AddConsumer<OutboxSampleConsumer>();

                x.AddEntityFrameworkOutbox<TestOutboxDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                    o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
                    o.QueryDelay = TimeSpan.FromMilliseconds(50);
                });

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(GetRabbitHost(), GetRabbitPort(), "/", h =>
                    {
                        h.Username(GetRabbitUser());
                        h.Password(GetRabbitPass());
                    });

                    cfg.UseProtobufTransit(registry);

                    cfg.ReceiveEndpoint(_queueName, e =>
                    {
                        e.UseProtobufTransit(registry);
                        e.UseEntityFrameworkOutbox<TestOutboxDbContext>(context);
                        e.ConfigureConsumer<OutboxSampleConsumer>(context);
                    });
                });
            });
        });

        _host = builder.Build();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await _host.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [Test]
    public async Task Outbox_StoresBase64InPostgres_AndDeliversBinaryProtobufOverRabbitMq()
    {
        if (_host == null)
        {
            Assert.Ignore("Integration host was not initialized.");
            return;
        }

        var expectedMessageId = Guid.NewGuid();
        var expectedCorrelationId = Guid.NewGuid();
        var nonAsciiText = "Transactional outbox verified with ProtobufTransit 🌟✨ В сказочной долине жил сонный ёжик 🦔";
        var message = new SampleTextMessage
        {
            Text = nonAsciiText,
            Count = 888
        };
        message.Tags.Add("outbox-verified");
        message.Tags.Add("transactional");
        message.Tags.Add("binary-wire");

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            await using var tx = await db.Database.BeginTransactionAsync();

            await publishEndpoint.Publish(message, ctx =>
            {
                ctx.MessageId = expectedMessageId;
                ctx.CorrelationId = expectedCorrelationId;
            });

            await db.SaveChangesAsync();

            var outboxMessage = await db.Set<OutboxMessage>()
                .OrderByDescending(x => x.SequenceNumber)
                .FirstOrDefaultAsync(x => x.ConversationId == null || x.CorrelationId == expectedCorrelationId);

            Assert.That(outboxMessage, Is.Not.Null, "OutboxMessage record must be present in database within transaction.");
            Assert.That(outboxMessage!.ContentType, Is.EqualTo("application/x-protobuf"));
            Assert.That(outboxMessage.Body, Is.Not.Null.And.Not.Empty);

            byte[] decodedBytes;
            try
            {
                decodedBytes = Convert.FromBase64String(outboxMessage.Body);
            }
            catch (FormatException ex)
            {
                Assert.Fail($"Outbox body in database is not valid Base64: {ex.Message}");
                return;
            }

            Assert.That(decodedBytes.Length, Is.GreaterThan(0));
            Assert.That(decodedBytes[0], Is.Not.EqualTo((byte)'{'));
            Assert.That(decodedBytes[0], Is.Not.EqualTo((byte)'['));

            var parsedFromOutbox = SampleTextMessage.Parser.ParseFrom(decodedBytes);
            Assert.That(parsedFromOutbox.Text, Is.EqualTo(nonAsciiText));
            Assert.That(parsedFromOutbox.Count, Is.EqualTo(888));
            Assert.That(parsedFromOutbox.Tags, Contains.Item("outbox-verified"));
            Assert.That(parsedFromOutbox.Tags, Contains.Item("binary-wire"));

            Assert.That(outboxMessage.Headers, Does.Contain(CustomCanonicalUrn));
            Assert.That(outboxMessage.Headers, Does.Contain(expectedCorrelationId.ToString()));

            await tx.CommitAsync();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var completed = await Task.WhenAny(ConsumerTcs.Task, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token));
        Assert.That(completed, Is.SameAs(ConsumerTcs.Task), "Timeout waiting for outbox message delivery via RabbitMQ.");

        var context = await ConsumerTcs.Task;
        Assert.That(context.MessageId, Is.EqualTo(expectedMessageId));
        Assert.That(context.CorrelationId, Is.EqualTo(expectedCorrelationId));
        Assert.That(context.Message.Text, Is.EqualTo(nonAsciiText));
        Assert.That(context.Message.Count, Is.EqualTo(888));
        Assert.That(context.Message.Tags, Contains.Item("outbox-verified"));
        Assert.That(context.Message.Tags, Contains.Item("transactional"));
        Assert.That(context.Message.Tags, Contains.Item("binary-wire"));
    }

    private static bool IsPostgresReachable()
    {
        try
        {
            using var conn = new NpgsqlConnection(GetPostgresConnection());
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRabbitMqReachable()
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(GetRabbitHost(), GetRabbitPort(), null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
            if (!success)
            {
                return false;
            }

            client.EndConnect(result);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public class OutboxSampleConsumer : IConsumer<SampleTextMessage>
    {
        public Task Consume(ConsumeContext<SampleTextMessage> context)
        {
            ConsumerTcs.TrySetResult(context);
            return Task.CompletedTask;
        }
    }
}

public class TestOutboxDbContext : DbContext
{
    public TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
