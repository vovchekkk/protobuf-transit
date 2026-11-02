using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ProtobufTransit.Tests.Messages;

namespace ProtobufTransit.Tests;

[TestFixture]
public class MixedSerializationTests
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var services = new ServiceCollection();

        var registry = new ProtobufMessageRegistryBuilder()
            .Register(SampleTextMessage.Parser, "urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage", "SampleTextMessage")
            .Build();

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<ProtobufMessageConsumer>();
            x.AddConsumer<JsonPocoMessageConsumer>();
            x.AddConsumer<FailingProtobufMessageConsumer>();
            x.AddConsumer<FaultMessageConsumer>();

            x.UsingInMemory((context, cfg) =>
            {
                cfg.UseProtobufTransit(registry);
                cfg.ConfigureEndpoints(context);
            });
        });

        _provider = services.BuildServiceProvider(true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    [Test]
    public async Task RegisteredProtobufMessage_SentAndConsumedSuccessfully()
    {
        await _harness.Bus.Publish(new SampleTextMessage
        {
            Text = "Protobuf over in-memory bus",
            Count = 101
        });

        Assert.That(await _harness.GetConsumerHarness<ProtobufMessageConsumer>().Consumed.Any<SampleTextMessage>(), Is.True);
    }

    [Test]
    public async Task UnregisteredJsonMessage_SentAndConsumedViaDefaultJsonSerializer()
    {
        await _harness.Bus.Publish(new JsonPocoMessage
        {
            Greeting = "Hello from standard MT JSON",
            Id = 555
        });

        Assert.That(await _harness.GetConsumerHarness<JsonPocoMessageConsumer>().Consumed.Any<JsonPocoMessage>(), Is.True);
    }

    [Test]
    public async Task FailingConsumer_ProducesNativeFault_HandledViaDefaultJsonSerializer()
    {
        await _harness.Bus.Publish(new SampleTextMessage
        {
            Text = "Trigger deliberate fault",
            Count = 999
        });

        Assert.That(await _harness.GetConsumerHarness<FailingProtobufMessageConsumer>().Consumed.Any<SampleTextMessage>(), Is.True);
        Assert.That(await _harness.GetConsumerHarness<FaultMessageConsumer>().Consumed.Any<Fault<SampleTextMessage>>(), Is.True);
    }

    public class ProtobufMessageConsumer : IConsumer<SampleTextMessage>
    {
        public Task Consume(ConsumeContext<SampleTextMessage> context)
        {
            if (context.Message.Count == 999)
            {
                return Task.CompletedTask;
            }

            Assert.That(context.Message.Text, Is.EqualTo("Protobuf over in-memory bus"));
            return Task.CompletedTask;
        }
    }

    public class JsonPocoMessageConsumer : IConsumer<JsonPocoMessage>
    {
        public Task Consume(ConsumeContext<JsonPocoMessage> context)
        {
            Assert.That(context.Message.Greeting, Is.EqualTo("Hello from standard MT JSON"));
            return Task.CompletedTask;
        }
    }

    public class FailingProtobufMessageConsumer : IConsumer<SampleTextMessage>
    {
        public Task Consume(ConsumeContext<SampleTextMessage> context)
        {
            if (context.Message.Count == 999)
            {
                throw new InvalidOperationException("Deliberate consumer failure for Fault<T> test.");
            }

            return Task.CompletedTask;
        }
    }

    public class FaultMessageConsumer : IConsumer<Fault<SampleTextMessage>>
    {
        public Task Consume(ConsumeContext<Fault<SampleTextMessage>> context)
        {
            Assert.That(context.Message.Message.Text, Is.EqualTo("Trigger deliberate fault"));
            Assert.That(context.Message.Exceptions, Is.Not.Empty);
            return Task.CompletedTask;
        }
    }
}

public class JsonPocoMessage
{
    public string Greeting { get; set; } = string.Empty;
    public int Id { get; set; }
}
