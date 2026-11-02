using System.Runtime.Serialization;
using Google.Protobuf;
using MassTransit;
using MassTransit.Context;
using NUnit.Framework;
using ProtobufTransit.Tests.Messages;

namespace ProtobufTransit.Tests;

[TestFixture]
public class ProtobufOutboxBase64Tests
{
    private IProtobufMessageRegistry _registry = null!;
    private ProtobufMessageSerializer _serializer = null!;
    private ProtobufMessageDeserializer _deserializer = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new ProtobufMessageRegistryBuilder()
            .Register(SampleTextMessage.Parser, "urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage", "SampleTextMessage")
            .Build();

        _serializer = new ProtobufMessageSerializer(_registry);
        _deserializer = new ProtobufMessageDeserializer(_registry);
    }

    [Test]
    public void OutboxMessageBody_Base64LosslessRoundtrip_WithCyrillicAndEmojis()
    {
        var complexText = "🌟✨ В сказочной долине жил сонный ёжик. Чай 🍵, звёзды 🌌, сон... 😴💤";
        var original = new SampleTextMessage
        {
            Text = complexText,
            Count = 99
        };
        original.Tags.Add("ёжик");
        original.Tags.Add("звёзды");
        original.Tags.Add("спящий");

        var sendContext = new MessageSendContext<SampleTextMessage>(original);
        var initialBody = _serializer.GetMessageBody(sendContext);
        var rawBytes = initialBody.GetBytes();

        var base64DbString = initialBody.GetString();
        Assert.That(base64DbString, Is.Not.Null.And.Not.Empty);

        var restoredBody = _deserializer.GetMessageBody(base64DbString);
        var restoredBytes = restoredBody.GetBytes();

        Assert.That(restoredBytes, Is.EqualTo(rawBytes));

        var serializerContext = _deserializer.Deserialize(restoredBody, sendContext.Headers);
        Assert.That(serializerContext.TryGetMessage<SampleTextMessage>(out var restoredMessage), Is.True);
        Assert.That(restoredMessage, Is.Not.Null);
        Assert.That(restoredMessage!.Text, Is.EqualTo(complexText));
        Assert.That(restoredMessage.Count, Is.EqualTo(99));
        Assert.That(restoredMessage.Tags.Count, Is.EqualTo(3));
        Assert.That(restoredMessage.Tags[0], Is.EqualTo("ёжик"));
    }

    [Test]
    public void OutboxForwarding_OriginalMessageBodyPreserved_WithoutReserialization()
    {
        var originalBytes = new byte[] { 0x0A, 0x04, 0x54, 0x65, 0x73, 0x74 };
        var storedBody = new ProtobufMessageBody(originalBytes);

        var sendContext = new MessageSendContext<MessageBody>(storedBody);
        var forwardedBody = _serializer.GetMessageBody(sendContext);

        Assert.That(forwardedBody, Is.SameAs(storedBody));
        Assert.That(forwardedBody.GetBytes(), Is.EqualTo(originalBytes));
    }

    [Test]
    public void CopyBodySerializer_ViaDeserializerSerializerContext_PreservesOriginalBodyBytesAndContentType()
    {
        var originalMessage = new SampleTextMessage
        {
            Text = "CopyBodySerializer preservation test",
            Count = 123
        };
        originalMessage.Tags.Add("unknown-field-guard");

        var rawBytes = originalMessage.ToByteArray();
        var body = new ProtobufMessageBody(rawBytes);

        var headers = new MessageSendContext<SampleTextMessage>(originalMessage).Headers;
        headers.Set(ProtobufConstants.MessageTypeHeader, "urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage");

        var serializerContext = _deserializer.Deserialize(body, headers);

        var copySerializer = serializerContext.GetMessageSerializer();
        Assert.That(copySerializer, Is.Not.Null);
        Assert.That(copySerializer.ContentType, Is.EqualTo(ProtobufConstants.ProtobufContentType));

        var sendContext = new MessageSendContext<SampleTextMessage>(originalMessage);
        var copiedBody = copySerializer.GetMessageBody(sendContext);

        Assert.That(copiedBody.GetBytes(), Is.EqualTo(rawBytes));
        Assert.That(copiedBody.GetString(), Is.EqualTo(Convert.ToBase64String(rawBytes)));
    }

    [Test]
    public void GetMessageBody_InvalidBase64_ThrowsSerializationException()
    {
        Assert.Throws<SerializationException>(() =>
            _deserializer.GetMessageBody("not-a-valid-base64-string!@#$"));
    }

    [TestCase(null)]
    [TestCase("")]
    public void GetMessageBody_EmptyOrNull_ReturnsEmptyMessageBody(string? emptyInput)
    {
        var body = _deserializer.GetMessageBody(emptyInput!);
        Assert.That(body.GetBytes().Length, Is.EqualTo(0));
    }

    [Test]
    public void ProtobufMessageBody_StreamAndProperties_BehaveCorrectly()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var body = new ProtobufMessageBody(bytes);

        Assert.That(body.Length, Is.EqualTo(5));
        Assert.That(body.GetBytes(), Is.EqualTo(bytes));

        using var stream = body.GetStream();
        Assert.That(stream.CanRead, Is.True);
        Assert.That(stream.CanWrite, Is.False);
        Assert.That(stream.Length, Is.EqualTo(5));

        var buffer = new byte[5];
        var read = stream.Read(buffer, 0, 5);
        Assert.That(read, Is.EqualTo(5));
        Assert.That(buffer, Is.EqualTo(bytes));
    }
}
