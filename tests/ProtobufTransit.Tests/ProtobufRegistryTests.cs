using Google.Protobuf;
using NUnit.Framework;
using ProtobufTransit.Tests.Messages;

namespace ProtobufTransit.Tests;

[TestFixture]
public class ProtobufRegistryTests
{
    [Test]
    public void Register_ValidTypes_PopulatesRegistrySuccessfully()
    {
        var registry = new ProtobufMessageRegistryBuilder()
            .Register(SampleTextMessage.Parser, "urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage", "SampleTextMessage")
            .Register(SampleBinaryMessage.Parser, "urn:message:ProtobufTransit.Tests.Messages:SampleBinaryMessage", "SampleBinaryMessage")
            .Build();

        Assert.That(registry.IsRegistered(typeof(SampleTextMessage)), Is.True);
        Assert.That(registry.IsRegistered(typeof(SampleBinaryMessage)), Is.True);
        Assert.That(registry.IsRegistered("urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage"), Is.True);
        Assert.That(registry.IsRegistered("SampleTextMessage"), Is.True);
        Assert.That(registry.IsRegistered("sampletextmessage"), Is.True);
        Assert.That(registry.Registrations.Count, Is.EqualTo(2));

        Assert.That(registry.TryGetRegistration(typeof(SampleTextMessage), out var reg1), Is.True);
        Assert.That(reg1, Is.Not.Null);
        Assert.That(reg1!.CanonicalUrn, Is.EqualTo("urn:message:ProtobufTransit.Tests.Messages:SampleTextMessage"));
        Assert.That(reg1.Aliases, Contains.Item("SampleTextMessage"));

        Assert.That(registry.TryGetRegistration("sampletextmessage", out var reg2), Is.True);
        Assert.That(reg2, Is.SameAs(reg1));
    }

    [Test]
    public void Register_DuplicateType_ThrowsArgumentException()
    {
        var builder = new ProtobufMessageRegistryBuilder();
        builder.Register(SampleTextMessage.Parser, "urn:message:Test:Message1");

        var ex = Assert.Throws<ArgumentException>(() =>
            builder.Register(SampleTextMessage.Parser, "urn:message:Test:Message2"));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("already registered"));
    }

    [Test]
    public void Register_DuplicateCanonicalUrn_ThrowsArgumentException()
    {
        var builder = new ProtobufMessageRegistryBuilder();
        builder.Register(SampleTextMessage.Parser, "urn:message:Test:DuplicateUrn");

        var ex = Assert.Throws<ArgumentException>(() =>
            builder.Register(SampleBinaryMessage.Parser, "urn:message:Test:DuplicateUrn"));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("already registered"));
    }

    [Test]
    public void Register_DuplicateCanonicalUrnDifferentCase_ThrowsArgumentException()
    {
        var builder = new ProtobufMessageRegistryBuilder();
        builder.Register(SampleTextMessage.Parser, "urn:message:Test:CaseUrn");

        var ex = Assert.Throws<ArgumentException>(() =>
            builder.Register(SampleBinaryMessage.Parser, "urn:message:test:caseurn"));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("already registered"));
    }

    [Test]
    public void Register_AliasIdenticalToCanonicalUrn_ThrowsArgumentException()
    {
        var builder = new ProtobufMessageRegistryBuilder();

        var ex = Assert.Throws<ArgumentException>(() =>
            builder.Register(SampleTextMessage.Parser, "urn:message:Test:Same", "urn:message:Test:Same"));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("cannot be identical"));
    }

    [Test]
    public void Register_DuplicateAliasWithinSameRegistration_ThrowsArgumentException()
    {
        var builder = new ProtobufMessageRegistryBuilder();

        var ex = Assert.Throws<ArgumentException>(() =>
            builder.Register(SampleTextMessage.Parser, "urn:message:Test:Msg", "AliasA", "aliasa"));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("Duplicate alias"));
    }

    [Test]
    public void Register_AliasCollidingWithOtherCanonicalUrn_ThrowsArgumentException()
    {
        var builder = new ProtobufMessageRegistryBuilder();
        builder.Register(SampleTextMessage.Parser, "urn:message:Test:PrimaryUrn");

        var ex = Assert.Throws<ArgumentException>(() =>
            builder.Register(SampleBinaryMessage.Parser, "urn:message:Test:Binary", "urn:message:Test:PrimaryUrn"));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("already registered"));
    }

    [Test]
    public void Register_AliasCollidingWithOtherAlias_ThrowsArgumentException()
    {
        var builder = new ProtobufMessageRegistryBuilder();
        builder.Register(SampleTextMessage.Parser, "urn:message:Test:Text", "SharedAlias");

        var ex = Assert.Throws<ArgumentException>(() =>
            builder.Register(SampleBinaryMessage.Parser, "urn:message:Test:Binary", "sharedalias"));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("already registered"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Register_InvalidCanonicalUrn_ThrowsArgumentException(string? invalidUrn)
    {
        var builder = new ProtobufMessageRegistryBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.Register(SampleTextMessage.Parser, invalidUrn!));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Register_InvalidAlias_ThrowsArgumentException(string invalidAlias)
    {
        var builder = new ProtobufMessageRegistryBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.Register(SampleTextMessage.Parser, "urn:message:Test:Msg", invalidAlias));
    }

    [Test]
    public void EmptyRegistry_BehavesCorrectly()
    {
        var registry = ProtobufMessageRegistry.Empty;

        Assert.That(registry.Registrations, Is.Empty);
        Assert.That(registry.IsRegistered(typeof(SampleTextMessage)), Is.False);
        Assert.That(registry.IsRegistered("any-urn"), Is.False);
        Assert.That(registry.TryGetRegistration(typeof(SampleTextMessage), out var reg1), Is.False);
        Assert.That(reg1, Is.Null);
        Assert.That(registry.TryGetRegistration("any-urn", out var reg2), Is.False);
        Assert.That(reg2, Is.Null);
    }

    [Test]
    public void CreateHelper_BuildsConfiguredRegistry()
    {
        var registry = ProtobufMessageRegistry.Create(b =>
        {
            b.Register(SampleTextMessage.Parser, "urn:message:Test:Text");
        });

        Assert.That(registry.IsRegistered(typeof(SampleTextMessage)), Is.True);
    }
}
