using Google.Protobuf;

namespace ProtobufTransit;

public sealed class ProtobufMessageRegistryBuilder
{
    private readonly Dictionary<Type, ProtobufMessageRegistration> _byType = new();
    private readonly Dictionary<string, ProtobufMessageRegistration> _byUrnOrAlias = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ProtobufMessageRegistration> _registrations = new();

    public ProtobufMessageRegistryBuilder Register<T>(
        MessageParser<T> parser,
        string canonicalUrn,
        params string[] aliases) where T : class, IMessage<T>
    {
        return Register(parser, canonicalUrn, (IEnumerable<string>)aliases);
    }

    public ProtobufMessageRegistryBuilder Register<T>(
        MessageParser<T> parser,
        string canonicalUrn,
        IEnumerable<string>? aliases = null) where T : class, IMessage<T>
    {
        ArgumentNullException.ThrowIfNull(parser);

        if (string.IsNullOrWhiteSpace(canonicalUrn))
        {
            throw new ArgumentException("Canonical URN cannot be null or whitespace.", nameof(canonicalUrn));
        }

        var messageType = typeof(T);
        if (_byType.ContainsKey(messageType))
        {
            throw new ArgumentException($"Type '{messageType.FullName}' is already registered in the Protobuf message registry.", nameof(parser));
        }

        if (_byUrnOrAlias.TryGetValue(canonicalUrn, out var existingWithUrn))
        {
            throw new ArgumentException($"Canonical URN '{canonicalUrn}' is already registered for type '{existingWithUrn.MessageType.FullName}'.", nameof(canonicalUrn));
        }

        var aliasList = new List<string>();
        if (aliases != null)
        {
            foreach (var alias in aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    throw new ArgumentException("Alias cannot be null or whitespace.", nameof(aliases));
                }

                if (string.Equals(alias, canonicalUrn, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Alias '{alias}' cannot be identical to canonical URN '{canonicalUrn}'.", nameof(aliases));
                }

                if (aliasList.Contains(alias, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Duplicate alias '{alias}' specified for type '{messageType.FullName}'.", nameof(aliases));
                }

                if (_byUrnOrAlias.TryGetValue(alias, out var existingWithAlias))
                {
                    throw new ArgumentException($"Alias '{alias}' is already registered for type '{existingWithAlias.MessageType.FullName}'.", nameof(aliases));
                }

                aliasList.Add(alias);
            }
        }

        var registration = new ProtobufMessageRegistration(
            messageType,
            canonicalUrn,
            aliasList.AsReadOnly(),
            parser);

        _byType.Add(messageType, registration);
        _byUrnOrAlias.Add(canonicalUrn, registration);
        foreach (var alias in aliasList)
        {
            _byUrnOrAlias.Add(alias, registration);
        }

        _registrations.Add(registration);
        return this;
    }

    public IProtobufMessageRegistry Build()
    {
        return new ProtobufMessageRegistry(
            new Dictionary<Type, ProtobufMessageRegistration>(_byType),
            new Dictionary<string, ProtobufMessageRegistration>(_byUrnOrAlias, StringComparer.OrdinalIgnoreCase),
            _registrations.ToArray());
    }
}
