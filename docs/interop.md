# Wire Format, Transactional Outbox & Cross-Language Interoperability

## 1. Wire Format Architecture

ProtobufTransit implements binary serialization for MassTransit 8.x endpoints while preserving native JSON serialization as the default fallback for non-protobuf messages, `Fault<T>`, and `ReceiveFault`.

### Binary Transport Wire Format

When sending or publishing messages to an AMQP broker (such as RabbitMQ):
- **Content Type**: `application/x-protobuf`
- **Body**: Raw Google.Protobuf binary payload (`IMessage.ToByteArray()`)

### Transactional Outbox Bridge

MassTransit Entity Framework Core Outbox requires message bodies to be persisted as text in database tables (`OutboxMessage.Body`).
- **Storage**: `ProtobufMessageBody.GetString()` serializes the binary protobuf payload into a Base64 string for database storage.
- **Delivery**: When the MassTransit Outbox dispatcher processes stored messages, `ProtobufMessageDeserializer.GetMessageBody(string)` decodes the Base64 string back into binary bytes. `ProtobufMessageSerializer` returns the existing `MessageBody` directly, streaming raw binary bytes to the broker without re-serialization.

### Wire Headers

ProtobufTransit resolves message contracts via transport headers:

| Header Name | Priority | Status | Purpose |
| --- | --- | --- | --- |
| `MT-MessageType` | 1 | Required (one of top 3) | Canonical message type URN |
| `MessageType` | 2 | Required (one of top 3) | Fallback canonical message type URN |
| `type` | 3 | Required (one of top 3) | Fallback canonical message type URN |
| `MT-MessageId` | - | Optional | Recommended stable GUID for outbox/inbox deduplication |
| `MessageId` | - | Optional | Fallback GUID for outbox/inbox deduplication |
| `MT-CorrelationId` | - | Optional | Correlation identifier GUID |
| `CorrelationId` | - | Optional | Fallback correlation identifier GUID |

During deserialization, `ProtobufMessageDeserializer` checks headers in strict priority order (`MT-MessageType`, then `MessageType`, then `type`). If none of these headers are present, deserialization fails closed by throwing `SerializationException`.

---

## 2. Python Interoperability (aio-pika)

Python services can publish protobuf messages directly to MassTransit endpoints via RabbitMQ using `aio-pika`.

### Prerequisites & Architecture Constraints

- **Topology Pre-Provisioning**: The destination exchange and queue bindings must already exist on the broker before publishing with `mandatory=True`. MassTransit provisions topology when starting receive endpoints, or topology can be pre-declared via broker configuration.
- **Connection Lifecycle**: In production applications, `RobustConnection` and `RobustChannel` instances should be instantiated during application startup and reused across requests.
- **Scope**: The snippet below demonstrates an interoperable AMQP publisher. It is not a complete worker framework with automated retry policies, inbox deduplication, or distributed transaction semantics.

### Environment Setup & Code Generation

From the repository root:

```shell
python -m pip install aio-pika protobuf grpcio-tools
python -m grpc_tools.protoc -I examples/python --python_out=examples/python examples/python/sample.proto
python examples/python/send_message.py
```

### Publisher Example

```python
import asyncio
import os
import uuid
import aio_pika
from sample_pb2 import OrderSubmitted

async def publish_message() -> None:
    amqp_url = os.getenv("RABBITMQ_URL", "amqp://demo:demo@127.0.0.1:56789/")
    message_id = str(uuid.uuid4())
    correlation_id = str(uuid.uuid4())
    canonical_urn = "urn:message:sample:OrderSubmitted"

    order = OrderSubmitted(order_id="ord-123", customer_id="cust-456")
    payload = order.SerializeToString()

    connection = await aio_pika.connect_robust(amqp_url, timeout=10)
    async with connection:
        channel = await connection.channel(publisher_confirms=True, on_return_raises=True)
        async with channel:
            exchange = await channel.get_exchange("order-submitted-queue", ensure=False)
            message = aio_pika.Message(
                body=payload,
                content_type="application/x-protobuf",
                delivery_mode=aio_pika.DeliveryMode.PERSISTENT,
                message_id=message_id,
                correlation_id=correlation_id,
                type=canonical_urn,
                headers={
                    "MT-MessageType": canonical_urn,
                    "MT-MessageId": message_id,
                    "MT-CorrelationId": correlation_id,
                    "MessageType": canonical_urn,
                    "MessageId": message_id,
                    "CorrelationId": correlation_id,
                    "type": canonical_urn,
                },
            )
            await asyncio.wait_for(exchange.publish(message, routing_key="", mandatory=True), timeout=15.0)

if __name__ == "__main__":
    asyncio.run(publish_message())
```

A standalone runnable script and schema are available in [examples/python/send_message.py](https://github.com/vovchekkk/protobuf-transit/blob/main/examples/python/send_message.py) and [examples/python/sample.proto](https://github.com/vovchekkk/protobuf-transit/blob/main/examples/python/sample.proto). A runnable .NET 8 host example is available in [examples/dotnet/ProtobufTransit.Example](https://github.com/vovchekkk/protobuf-transit/blob/main/examples/dotnet/ProtobufTransit.Example).
