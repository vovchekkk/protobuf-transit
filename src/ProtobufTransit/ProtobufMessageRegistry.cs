using System.Diagnostics.CodeAnalysis;

namespace ProtobufTransit;

public sealed class ProtobufMessageRegistry : IProtobufMessageRegistry
{
    public static IProtobufMessageRegistry Empty { get; } = new ProtobufMessageRegistry(
        new Dictionary<Type, ProtobufMessageRegistration>(),
        new Dictionary<string, ProtobufMessageRegistration>(StringComparer.OrdinalIgnoreCase),
        Array.Empty<ProtobufMessageRegistration>());

    private readonly IReadOnlyDictionary<Type, ProtobufMessageRegistration> _byType;
    private readonly IReadOnlyDictionary<string, ProtobufMessageRegistration> _byUrnOrAlias;
    private readonly IReadOnlyCollection<ProtobufMessageRegistration> _registrations;

    internal ProtobufMessageRegistry(
        IReadOnlyDictionary<Type, ProtobufMessageRegistration> byType,
        IReadOnlyDictionary<string, ProtobufMessageRegistration> byUrnOrAlias,
        IReadOnlyCollection<ProtobufMessageRegistration> registrations)
    {
        _byType = byType;
        _byUrnOrAlias = byUrnOrAlias;
        _registrations = registrations;
    }

    public static IProtobufMessageRegistry Create(Action<ProtobufMessageRegistryBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ProtobufMessageRegistryBuilder();
        configure(builder);
        return builder.Build();
    }

    public bool TryGetRegistration(Type messageType, [NotNullWhen(true)] out ProtobufMessageRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return _byType.TryGetValue(messageType, out registration);
    }

    public bool TryGetRegistration(string urnOrAlias, [NotNullWhen(true)] out ProtobufMessageRegistration? registration)
    {
        if (string.IsNullOrWhiteSpace(urnOrAlias))
        {
            registration = null;
            return false;
        }

        return _byUrnOrAlias.TryGetValue(urnOrAlias, out registration);
    }

    public bool IsRegistered(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return _byType.ContainsKey(messageType);
    }

    public bool IsRegistered(string urnOrAlias)
    {
        if (string.IsNullOrWhiteSpace(urnOrAlias))
        {
            return false;
        }

        return _byUrnOrAlias.ContainsKey(urnOrAlias);
    }

    public IReadOnlyCollection<ProtobufMessageRegistration> Registrations => _registrations;
}
