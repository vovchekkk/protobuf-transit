using Google.Protobuf;
using MassTransit;
using MassTransit.Context;
using NUnit.Framework;
using ProtobufTransit.Tests.Messages;

namespace ProtobufTransit.Tests;

[TestFixture]
public class ProtobufRoundtripTests
{
    private IProtobufMessageRegistry _registry = null!;
    private ProtobufMessageSerializer _serializer = null!;
    private ProtobufMessageDeserializer _deserializer = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new ProtobufMessageRegistryBuilder()
            .Register(SampleTextMessage.Parser, "urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage", "SampleTextMessage")
            .Register(SampleBinaryMessage.Parser, "urn:message:ProtobufTransit.Tests.Messages:SampleBinaryMessage", "SampleBinaryMessage")
            .Build();

        _serializer = new ProtobufMessageSerializer(_registry);
        _deserializer = new ProtobufMessageDeserializer(_registry);
    }

    [Test]
    public void SampleTextMessage_Roundtrip_PreservesAllFieldsAndHeaders()
    {
        var expectedCorrelationId = Guid.NewGuid();
        var message = new SampleTextMessage
        {
            Text = "Roundtrip test message with special characters: 🌟 äöü ñ",
            Count = 42
        };
        message.Tags.Add("alpha");
        message.Tags.Add("beta");
        message.Tags.Add("gamma");

        var sendContext = new MessageSendContext<SampleTextMessage>(message)
        {
            CorrelationId = expectedCorrelationId
        };

        var body = _serializer.GetMessageBody(sendContext);
        var bytes = body.GetBytes();

        Assert.That(sendContext.ContentType, Is.EqualTo(ProtobufConstants.ProtobufContentType));
        Assert.That(sendContext.MessageId, Is.Not.Null);
        Assert.That(sendContext.CorrelationId, Is.EqualTo(expectedCorrelationId));
        Assert.That(sendContext.Headers.Get<string>(ProtobufConstants.MessageTypeHeader), Is.EqualTo("urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage"));
        Assert.That(sendContext.Headers.Get<string>("MessageType"), Is.EqualTo("urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage"));
        Assert.That(sendContext.Headers.Get<string>("type"), Is.EqualTo("urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage"));
        Assert.That(sendContext.Headers.Get<string>("MessageId"), Is.EqualTo(sendContext.MessageId!.Value.ToString()));
        Assert.That(sendContext.Headers.Get<string>("CorrelationId"), Is.EqualTo(expectedCorrelationId.ToString()));

        Assert.That(bytes.Length, Is.GreaterThan(0));
        Assert.That(bytes[0], Is.Not.EqualTo((byte)'{'));
        Assert.That(bytes[0], Is.Not.EqualTo((byte)'[')) ;

        var serializerContext = _deserializer.Deserialize(body, sendContext.Headers);

        Assert.That(serializerContext.MessageId, Is.EqualTo(sendContext.MessageId));
        Assert.That(serializerContext.CorrelationId, Is.EqualTo(expectedCorrelationId));
        Assert.That(serializerContext.SupportedMessageTypes, Contains.Item("urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage"));
        Assert.That(serializerContext.SupportedMessageTypes, Contains.Item("SampleTextMessage"));

        Assert.That(serializerContext.IsSupportedMessageType<SampleTextMessage>(), Is.True);
        Assert.That(serializerContext.IsSupportedMessageType(typeof(SampleTextMessage)), Is.True);
        Assert.That(serializerContext.IsSupportedMessageType<SampleBinaryMessage>(), Is.False);

        Assert.That(serializerContext.TryGetMessage<SampleTextMessage>(out var restored), Is.True);
        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!.Text, Is.EqualTo(message.Text));
        Assert.That(restored.Count, Is.EqualTo(42));
        Assert.That(restored.Tags.Count, Is.EqualTo(3));
        Assert.That(restored.Tags, Is.EqualTo(new[] { "alpha", "beta", "gamma" }));

        Assert.That(serializerContext.TryGetMessage(typeof(SampleTextMessage), out var untypedRestored), Is.True);
        Assert.That(untypedRestored, Is.InstanceOf<SampleTextMessage>());

        Assert.That(serializerContext.TryGetMessage<SampleBinaryMessage>(out var wrongType), Is.False);
        Assert.That(wrongType, Is.Null);
    }

    [Test]
    public void SampleBinaryMessage_Roundtrip_PreservesRawBytePayload()
    {
        var rawData = new byte[256];
        for (int i = 0; i < rawData.Length; i++)
        {
            rawData[i] = (byte)(i % 256);
        }

        var message = new SampleBinaryMessage
        {
            Payload = ByteString.CopyFrom(rawData),
            ContentType = "application/octet-stream"
        };

        var sendContext = new MessageSendContext<SampleBinaryMessage>(message);
        var body = _serializer.GetMessageBody(sendContext);

        var serializerContext = _deserializer.Deserialize(body, sendContext.Headers);
        Assert.That(serializerContext.TryGetMessage<SampleBinaryMessage>(out var restored), Is.True);
        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!.ContentType, Is.EqualTo("application/octet-stream"));
        Assert.That(restored.Payload.ToByteArray(), Is.EqualTo(rawData));
    }

    [Test]
    public void Deserialization_ViaAlias_ResolvesCorrectType()
    {
        var message = new SampleTextMessage
        {
            Text = "Alias resolution test",
            Count = 7
        };

        var sendContext = new MessageSendContext<SampleTextMessage>(message);
        var body = _serializer.GetMessageBody(sendContext);

        sendContext.Headers.Set(ProtobufConstants.MessageTypeHeader, "SampleTextMessage");

        var serializerContext = _deserializer.Deserialize(body, sendContext.Headers);
        Assert.That(serializerContext.TryGetMessage<SampleTextMessage>(out var restored), Is.True);
        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!.Text, Is.EqualTo("Alias resolution test"));
    }

    [Test]
    public void SerializerFactory_CreatesMatchingSerializerAndDeserializer()
    {
        var factory = new ProtobufSerializerFactory(_registry);

        Assert.That(factory.ContentType, Is.EqualTo(ProtobufConstants.ProtobufContentType));
        Assert.That(factory.CreateSerializer(), Is.InstanceOf<ProtobufMessageSerializer>());
        Assert.That(factory.CreateDeserializer(), Is.InstanceOf<ProtobufMessageDeserializer>());
    }
}
