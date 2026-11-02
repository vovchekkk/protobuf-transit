# ProtobufTransit

Contract-agnostic Google.Protobuf binary serialization and Base64 transactional outbox bridge for MassTransit 8.x.

## Status & Compatibility

- **Status**: Alpha (`0.1.0-alpha.1`)
- **Runtime**: .NET 8 (`net8.0`)
- **MassTransit**: Tested on `8.3.6`. Dependency constraint is `[8.3.6, 9.0.0)`. MassTransit 9.x is unverified.
- **Google.Protobuf**: Tested on `3.26.1`.

Registered protobuf messages transmit over the wire as raw protobuf binary bytes (`application/x-protobuf`). MassTransit JSON serialization remains active as the fallback for unregistered message types, `Fault<T>`, and `ReceiveFault`.

---

## Installation

ProtobufTransit is not yet published to the public NuGet.org gallery. To build and consume the package directly from artifacts without modifying global NuGet configuration:

```shell
dotnet pack src/ProtobufTransit/ProtobufTransit.csproj -c Release -o ./artifacts
dotnet add <path-to-your-project>/<project>.csproj package ProtobufTransit --version 0.1.0-alpha.1 --source <absolute-path-to-protobuf-transit>/artifacts
```

After future public registration, installation will use:

```shell
dotnet add package ProtobufTransit --version 0.1.0-alpha.1
```

---

## Quickstart

### 1. Protobuf Contract & Code Generation

Add `Grpc.Tools` to your consuming project's `.csproj` to compile contracts automatically:

```xml
<ItemGroup>
  <PackageReference Include="Google.Protobuf" Version="3.26.1" />
  <PackageReference Include="Grpc.Tools" Version="2.66.0" PrivateAssets="All" />
  <Protobuf Include="Protos\sample.proto" GrpcServices="None" />
</ItemGroup>
```

Contract definition (`Protos/sample.proto`):

```protobuf
syntax = "proto3";

package sample;

option csharp_namespace = "Sample";

message OrderSubmitted {
  string order_id = 1;
  string customer_id = 2;
}
```

### 2. Registry Configuration

```csharp
using MassTransit;
using ProtobufTransit;
using Sample;

var registry = new ProtobufMessageRegistryBuilder()
    .Register(
        OrderSubmitted.Parser,
        canonicalUrn: "urn:message:sample:OrderSubmitted",
        aliases: new[] { "OrderSubmitted" })
    .Build();
```

### 3. Bus & Endpoint Setup

```csharp
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
```

### 4. Publish & Consume

```csharp
public async Task SendAsync(IPublishEndpoint publishEndpoint, Guid correlationId)
{
    await publishEndpoint.Publish(new OrderSubmitted
    {
        OrderId = "ord-123",
        CustomerId = "cust-456"
    }, ctx =>
    {
        ctx.CorrelationId = correlationId;
    });
}

public class OrderSubmittedConsumer : IConsumer<OrderSubmitted>
{
    public Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        var message = context.Message;
        return Task.CompletedTask;
    }
}
```

### 5. Runnable Examples

- **.NET 8 Host**: [examples/dotnet/ProtobufTransit.Example](https://github.com/vovchekkk/protobuf-transit/blob/main/examples/dotnet/ProtobufTransit.Example)
- **Python Publisher**: [examples/python/send_message.py](https://github.com/vovchekkk/protobuf-transit/blob/main/examples/python/send_message.py)

Run the C# smoke test:

```shell
dotnet run --project examples/dotnet/ProtobufTransit.Example -c Release -- --once
```

---

## Documentation Links

- [Wire Format, Outbox Bridge & Python Interoperability](https://github.com/vovchekkk/protobuf-transit/blob/main/docs/interop.md)
- [Runnable .NET Example](https://github.com/vovchekkk/protobuf-transit/blob/main/examples/dotnet/ProtobufTransit.Example)
- [Contributing Guidelines](https://github.com/vovchekkk/protobuf-transit/blob/main/CONTRIBUTING.md)
- [Security Policy](https://github.com/vovchekkk/protobuf-transit/blob/main/SECURITY.md)

---

## License & Notices

ProtobufTransit is licensed under the [MIT License](https://github.com/vovchekkk/protobuf-transit/blob/main/LICENSE) (c) 2026 vovchekkk.

This is an independent open-source library and is not endorsed by or affiliated with the MassTransit project or Google.

Third-party dependencies retain their respective licenses:
- MassTransit: Apache License 2.0
- Google.Protobuf: BSD 3-Clause License
- Microsoft.SourceLink.GitHub: MIT License
