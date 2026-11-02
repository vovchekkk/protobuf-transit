using System.Net.Mime;
using System.Runtime.Serialization;
using Google.Protobuf;
using MassTransit;
using MassTransit.Serialization;

namespace ProtobufTransit;

internal sealed class ProtobufMessageDeserializer : IMessageDeserializer
{
    private readonly IProtobufMessageRegistry _registry;

    public ProtobufMessageDeserializer(IProtobufMessageRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ContentType ContentType => ProtobufConstants.ProtobufContentType;

    public void Probe(ProbeContext context)
    {
        context.CreateScope("protobuf");
    }

    public ConsumeContext Deserialize(ReceiveContext receiveContext)
    {
        ArgumentNullException.ThrowIfNull(receiveContext);

        var rawContext = new RawMessageContext(receiveContext.TransportHeaders, receiveContext.InputAddress, RawSerializerOptions.Default);
        var (message, supportedTypes) = ParsePayload(receiveContext.Body.GetBytes(), receiveContext.TransportHeaders);
        var serializerContext = new ProtobufSerializerContext(message, rawContext, supportedTypes, receiveContext.Body, _registry);
        return new BodyConsumeContext(receiveContext, serializerContext);
    }

    public SerializerContext Deserialize(MessageBody body, Headers headers, Uri? destinationAddress = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(headers);

        var rawContext = new RawMessageContext(headers, destinationAddress, RawSerializerOptions.Default);
        var (message, supportedTypes) = ParsePayload(body.GetBytes(), headers);
        return new ProtobufSerializerContext(message, rawContext, supportedTypes, body, _registry);
    }

    public MessageBody GetMessageBody(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new ProtobufMessageBody(Array.Empty<byte>());
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(text);
            return new ProtobufMessageBody(bytes);
        }
        catch (FormatException ex)
        {
            throw new SerializationException("Failed to decode Base64 protobuf body.", ex);
        }
    }

    private (IMessage Message, string[] SupportedTypes) ParsePayload(byte[]? bytes, Headers headers)
    {
        if (bytes == null)
        {
            throw new SerializationException("Payload bytes cannot be null.");
        }

        var msgType = headers.Get<string>(ProtobufConstants.MessageTypeHeader)
                      ?? headers.Get<string>("MessageType")
                      ?? headers.Get<string>("type")
                      ?? headers.Get<string>("message_type");

        if (string.IsNullOrWhiteSpace(msgType))
        {
            throw new SerializationException("Missing message type header for protobuf deserialization.");
        }

        if (!_registry.TryGetRegistration(msgType, out var registration))
        {
            throw new SerializationException($"Message type '{msgType}' is not registered in the Protobuf message registry.");
        }

        IMessage message;
        try
        {
            message = registration.Parser.ParseFrom(bytes);
        }
        catch (InvalidProtocolBufferException ex)
        {
            throw new SerializationException($"Failed to deserialize protobuf payload for '{registration.CanonicalUrn}'.", ex);
        }

        var supported = new List<string>(1 + registration.Aliases.Count)
        {
            registration.CanonicalUrn
        };
        supported.AddRange(registration.Aliases);

        return (message, supported.ToArray());
    }
}
