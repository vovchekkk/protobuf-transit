import asyncio
import os
import uuid
import aio_pika
from sample_pb2 import OrderSubmitted

async def main() -> None:
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
    asyncio.run(main())
