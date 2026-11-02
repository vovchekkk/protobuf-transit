using System.Runtime.Serialization;
using Google.Protobuf;
using MassTransit;
using MassTransit.Context;
using NUnit.Framework;
using ProtobufTransit.Tests.Messages;

namespace ProtobufTransit.Tests;

[TestFixture]
public class ProtobufHeaderAndErrorTests
{
    private IProtobufMessageRegistry _registry = null!;
    private ProtobufMessageSerializer _serializer = null!;
    private ProtobufMessageDeserializer _deserializer = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new ProtobufMessageRegistryBuilder()
            .Register(SampleTextMessage.Parser, "urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage", "SampleTextMessage")
            .Register(SampleEmptyMessage.Parser, "urn:message:ProtobufTransit.Tests.Messages:SampleEmptyMessage", "SampleEmptyMessage")
            .Build();

        _serializer = new ProtobufMessageSerializer(_registry);
        _deserializer = new ProtobufMessageDeserializer(_registry);
    }

    [Test]
    public void UnknownHeader_ThrowsSerializationException()
    {
        var validMsg = new SampleTextMessage { Text = "Test" };
        var body = new ProtobufMessageBody(validMsg.ToByteArray());

        var headers = new MessageSendContext<SampleTextMessage>(validMsg).Headers;
        headers.Set(ProtobufConstants.MessageTypeHeader, "urn:message:ProtobufTransit:UnknownMessage");

        var ex = Assert.Throws<SerializationException>(() => _deserializer.Deserialize(body, headers));
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("not registered"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void MissingOrWhitespaceHeader_FailsClosedWithSerializationException(string? headerValue)
    {
        var validMsg = new SampleTextMessage { Text = "Test" };
        var body = new ProtobufMessageBody(validMsg.ToByteArray());

        var headers = new MessageSendContext<SampleTextMessage>(validMsg).Headers;
        if (headerValue != null)
        {
            headers.Set(ProtobufConstants.MessageTypeHeader, headerValue);
        }

        var ex = Assert.Throws<SerializationException>(() => _deserializer.Deserialize(body, headers));
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("Missing message type header"));
    }

    [Test]
    public void SpoofedHeader_RejectedWithSerializationException()
    {
        var validMsg = new SampleTextMessage { Text = "Test" };
        var body = new ProtobufMessageBody(validMsg.ToByteArray());

        var headers = new MessageSendContext<SampleTextMessage>(validMsg).Headers;
        headers.Set(ProtobufConstants.MessageTypeHeader, "urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage:spoofed");

        Assert.Throws<SerializationException>(() => _deserializer.Deserialize(body, headers));
    }

    [Test]
    public void MalformedBytes_ThrowsSerializationException()
    {
        var corruptBytes = new byte[] { 0x0A, 0xFF, 0x00, 0x01 };
        var body = new ProtobufMessageBody(corruptBytes);

        var headers = new MessageSendContext<SampleTextMessage>(new SampleTextMessage()).Headers;
        headers.Set(ProtobufConstants.MessageTypeHeader, "urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage");

        Assert.Throws<SerializationException>(() => _deserializer.Deserialize(body, headers));
    }

    [Test]
    public void ValidEmptyProtobuf_AcceptedAndDeserialized()
    {
        var emptyMessage = new SampleEmptyMessage();
        var emptyBytes = emptyMessage.ToByteArray();
        Assert.That(emptyBytes.Length, Is.EqualTo(0));

        var sendContext = new MessageSendContext<SampleEmptyMessage>(emptyMessage);
        var body = _serializer.GetMessageBody(sendContext);

        Assert.That(body.GetBytes().Length, Is.EqualTo(0));

        var serializerContext = _deserializer.Deserialize(body, sendContext.Headers);
        Assert.That(serializerContext.TryGetMessage<SampleEmptyMessage>(out var restored), Is.True);
        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!.OptionalText, Is.Empty);
    }

    [Test]
    public void NonProtobufPoco_ThrowsInvalidOperationException()
    {
        var nonProto = new PlainOldClass { Value = "hello" };
        var sendContext = new MessageSendContext<PlainOldClass>(nonProto);

        var ex = Assert.Throws<InvalidOperationException>(() => _serializer.GetMessageBody(sendContext));
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("not a Google.Protobuf message"));
    }

    [Test]
    public void UnregisteredProtobufType_ThrowsInvalidOperationException()
    {
        var unregistered = new SampleBinaryMessage();
        var sendContext = new MessageSendContext<SampleBinaryMessage>(unregistered);

        var ex = Assert.Throws<InvalidOperationException>(() => _serializer.GetMessageBody(sendContext));
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("not registered"));
    }
}

public class PlainOldClass
{
    public string Value { get; set; } = string.Empty;
}
