using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProtobufTransit;
using Sample;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        var registry = new ProtobufMessageRegistryBuilder()
            .Register(
                OrderSubmitted.Parser,
                canonicalUrn: "urn:message:sample:OrderSubmitted",
                aliases: new[] { "OrderSubmitted" })
            .Build();

        services.AddSingleton(registry);

        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(20);
            options.StopTimeout = TimeSpan.FromSeconds(10);
        });

        services.AddMassTransit(x =>
        {
            x.AddConsumer<OrderSubmittedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host("127.0.0.1", 56789, "/", h =>
                {
                    h.Username("demo");
                    h.Password("demo");
                });

                cfg.UseProtobufTransit(registry);

                cfg.ReceiveEndpoint("order-submitted-queue", e =>
                {
                    e.UseProtobufTransit(registry);
                    e.ConfigureConsumer<OrderSubmittedConsumer>(context);
                });
            });
        });
    })
    .Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ProtobufTransit.Example");

if (args.Contains("--once"))
{
    await host.StartAsync();
    try
    {
        var correlationId = Guid.NewGuid();
        var publishEndpoint = host.Services.GetRequiredService<IPublishEndpoint>();

        logger.LogInformation("Publishing OrderSubmitted with CorrelationId={CorrelationId}...", correlationId);

        await MessageSender.PublishOrderSubmittedAsync(publishEndpoint, correlationId, "ord-csharp-once", "cust-csharp-once");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var received = await ConsumerTracker.WaitForMessageAsync(cts.Token);

        if (received.CorrelationId != correlationId)
        {
            logger.LogError("CorrelationId mismatch: expected {Expected}, got {Actual}", correlationId, received.CorrelationId);
            return 1;
        }

        logger.LogInformation("Successfully verified OrderSubmitted roundtrip via MassTransit and ProtobufTransit.");
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Smoke test failed: {Message}", ex.Message);
        return 1;
    }
    finally
    {
        await host.StopAsync();
    }
}

if (args.Contains("--receive-once"))
{
    await host.StartAsync();
    try
    {
        logger.LogInformation("Waiting up to 30s to receive one OrderSubmitted message on order-submitted-queue...");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var received = await ConsumerTracker.WaitForMessageAsync(cts.Token);

        logger.LogInformation("Successfully received message in --receive-once mode: OrderId={OrderId}, CustomerId={CustomerId}, CorrelationId={CorrelationId}",
            received.Message.OrderId,
            received.Message.CustomerId,
            received.CorrelationId);

        return 0;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Receive verification failed: {Message}", ex.Message);
        return 1;
    }
    finally
    {
        await host.StopAsync();
    }
}

logger.LogInformation("Starting ProtobufTransit host in continuous receive mode. Press Ctrl+C to shut down.");
await host.RunAsync();
return 0;

public static class MessageSender
{
    public static async Task SendOrderSubmittedAsync(ISendEndpointProvider sendEndpointProvider, Guid correlationId, string orderId, string customerId)
    {
        var sendEndpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:order-submitted-queue"));
        await sendEndpoint.Send(new OrderSubmitted
        {
            OrderId = orderId,
            CustomerId = customerId
        }, ctx =>
        {
            ctx.CorrelationId = correlationId;
        });
    }

    public static async Task PublishOrderSubmittedAsync(IPublishEndpoint publishEndpoint, Guid correlationId, string orderId, string customerId)
    {
        await publishEndpoint.Publish(new OrderSubmitted
        {
            OrderId = orderId,
            CustomerId = customerId
        }, ctx =>
        {
            ctx.CorrelationId = correlationId;
        });
    }
}

public sealed class OrderSubmittedConsumer : IConsumer<OrderSubmitted>
{
    private readonly ILogger<OrderSubmittedConsumer> _logger;

    public OrderSubmittedConsumer(ILogger<OrderSubmittedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Consumed OrderSubmitted: OrderId={OrderId}, CustomerId={CustomerId}, MessageId={MessageId}, CorrelationId={CorrelationId}",
            message.OrderId,
            message.CustomerId,
            context.MessageId,
            context.CorrelationId);

        ConsumerTracker.Notify(context);
        return Task.CompletedTask;
    }
}

internal static class ConsumerTracker
{
    private static readonly TaskCompletionSource<ConsumeContext<OrderSubmitted>> CompletionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static Task<ConsumeContext<OrderSubmitted>> WaitForMessageAsync(CancellationToken cancellationToken)
    {
        return CompletionSource.Task.WaitAsync(cancellationToken);
    }

    public static void Notify(ConsumeContext<OrderSubmitted> context)
    {
        CompletionSource.TrySetResult(context);
    }
}
