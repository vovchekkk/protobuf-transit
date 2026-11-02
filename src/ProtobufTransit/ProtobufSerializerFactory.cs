using System.Net.Mime;
using MassTransit;

namespace ProtobufTransit;

internal sealed class ProtobufSerializerFactory : ISerializerFactory
{
    private readonly IProtobufMessageRegistry _registry;

    public ProtobufSerializerFactory(IProtobufMessageRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ContentType ContentType => ProtobufConstants.ProtobufContentType;

    public IMessageSerializer CreateSerializer() => new ProtobufMessageSerializer(_registry);

    public IMessageDeserializer CreateDeserializer() => new ProtobufMessageDeserializer(_registry);
}
