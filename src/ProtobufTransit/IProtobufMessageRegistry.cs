using System.Diagnostics.CodeAnalysis;

namespace ProtobufTransit;

public interface IProtobufMessageRegistry
{
    bool TryGetRegistration(Type messageType, [NotNullWhen(true)] out ProtobufMessageRegistration? registration);
    bool TryGetRegistration(string urnOrAlias, [NotNullWhen(true)] out ProtobufMessageRegistration? registration);
    bool IsRegistered(Type messageType);
    bool IsRegistered(string urnOrAlias);
    IReadOnlyCollection<ProtobufMessageRegistration> Registrations { get; }
}
