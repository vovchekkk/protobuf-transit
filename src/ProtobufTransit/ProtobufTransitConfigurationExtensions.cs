using MassTransit;

namespace ProtobufTransit;

public static class ProtobufTransitConfigurationExtensions
{
    public static IBusFactoryConfigurator UseProtobufTransit(
        this IBusFactoryConfigurator configurator,
        IProtobufMessageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(registry);

        var factory = new ProtobufSerializerFactory(registry);
        configurator.AddDeserializer(factory, isDefault: false);

        var serializer = new ProtobufMessageSerializer(registry);

        configurator.ConfigureSend(sendPipeConfigurator =>
        {
            sendPipeConfigurator.UseExecute(sendContext =>
            {
                ApplyProtobufSerializer(sendContext, registry, serializer);
            });
        });

        configurator.ConfigurePublish(publishPipeConfigurator =>
        {
            publishPipeConfigurator.UseExecute(publishContext =>
            {
                ApplyProtobufSerializer(publishContext, registry, serializer);
            });
        });

        return configurator;
    }

    public static IBusFactoryConfigurator UseProtobufTransit(
        this IBusFactoryConfigurator configurator,
        Action<ProtobufMessageRegistryBuilder> configureRegistry)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(configureRegistry);

        var builder = new ProtobufMessageRegistryBuilder();
        configureRegistry(builder);
        return configurator.UseProtobufTransit(builder.Build());
    }

    public static IReceiveEndpointConfigurator UseProtobufTransit(
        this IReceiveEndpointConfigurator configurator,
        IProtobufMessageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(registry);

        var factory = new ProtobufSerializerFactory(registry);
        configurator.AddDeserializer(factory, isDefault: false);
        return configurator;
    }

    public static IReceiveEndpointConfigurator UseProtobufTransit(
        this IReceiveEndpointConfigurator configurator,
        Action<ProtobufMessageRegistryBuilder> configureRegistry)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(configureRegistry);

        var builder = new ProtobufMessageRegistryBuilder();
        configureRegistry(builder);
        return configurator.UseProtobufTransit(builder.Build());
    }

    private static void ApplyProtobufSerializer(SendContext context, IProtobufMessageRegistry registry, ProtobufMessageSerializer serializer)
    {
        var messageType = ResolveMessageType(context);
        if (messageType != null && registry.IsRegistered(messageType))
        {
            context.Serializer = serializer;
            return;
        }

        if (context.SupportedMessageTypes.Any(registry.IsRegistered))
        {
            context.Serializer = serializer;
            return;
        }

        if (context.ContentType != null &&
            string.Equals(context.ContentType.MediaType, ProtobufConstants.ContentTypeString, StringComparison.OrdinalIgnoreCase))
        {
            context.Serializer = serializer;
        }
    }

    private static Type? ResolveMessageType(SendContext context)
    {
        var type = context.GetType();
        while (type != null && type != typeof(object))
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SendContext<>))
            {
                return type.GetGenericArguments()[0];
            }

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(SendContext<>))
                {
                    return iface.GetGenericArguments()[0];
                }
            }

            type = type.BaseType;
        }

        return null;
    }
}
