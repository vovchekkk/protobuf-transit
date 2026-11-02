using System.Net.Mime;
using Google.Protobuf;
using MassTransit;

namespace ProtobufTransit;

internal sealed class ProtobufMessageSerializer : IMessageSerializer
{
    private readonly IProtobufMessageRegistry _registry;

    public ProtobufMessageSerializer(IProtobufMessageRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ContentType ContentType => ProtobufConstants.ProtobufContentType;

    public MessageBody GetMessageBody<T>(SendContext<T> context) where T : class
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Message is MessageBody messageBody)
        {
            return messageBody;
        }

        if (context.Message is not IMessage message)
        {
            throw new InvalidOperationException($"Type '{typeof(T).FullName}' is not a Google.Protobuf message.");
        }

        if (!_registry.TryGetRegistration(message.GetType(), out var registration))
        {
            throw new InvalidOperationException($"Message type '{message.GetType().FullName}' is not registered in the Protobuf message registry.");
        }

        context.ContentType = ProtobufConstants.ProtobufContentType;
        context.Headers.Set(ProtobufConstants.MessageTypeHeader, registration.CanonicalUrn);
        context.Headers.Set("MessageType", registration.CanonicalUrn);
        context.Headers.Set("type", registration.CanonicalUrn);

        context.MessageId ??= NewId.NextGuid();
        context.Headers.Set(MessageHeaders.MessageId, context.MessageId.Value.ToString());
        context.Headers.Set("MT-MessageId", context.MessageId.Value.ToString());
        context.Headers.Set("MessageId", context.MessageId.Value.ToString());

        if (context.CorrelationId.HasValue)
        {
            context.Headers.Set(MessageHeaders.CorrelationId, context.CorrelationId.Value.ToString());
            context.Headers.Set("MT-CorrelationId", context.CorrelationId.Value.ToString());
            context.Headers.Set("CorrelationId", context.CorrelationId.Value.ToString());
        }

        byte[] bytes = message.ToByteArray();
        return new ProtobufMessageBody(bytes);
    }
}
