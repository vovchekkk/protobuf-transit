using Google.Protobuf;

namespace ProtobufTransit;

public sealed class ProtobufMessageRegistration
{
    public Type MessageType { get; }
    public string CanonicalUrn { get; }
    public IReadOnlyList<string> Aliases { get; }
    public MessageParser Parser { get; }

    public ProtobufMessageRegistration(
        Type messageType,
        string canonicalUrn,
        IReadOnlyList<string> aliases,
        MessageParser parser)
    {
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
        CanonicalUrn = canonicalUrn ?? throw new ArgumentNullException(nameof(canonicalUrn));
        Aliases = aliases ?? throw new ArgumentNullException(nameof(aliases));
        Parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }
}
