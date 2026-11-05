using System.Text.Json;
using MassTransit;
using MassTransit.Serialization;

namespace ProtobufTransit;

internal sealed class ProtobufSerializerContext : SerializerContext
{
    private readonly object _message;
    private readonly MessageContext _context;
    private readonly string[] _supportedMessageTypes;
    private readonly MessageBody? _body;
    private readonly IProtobufMessageRegistry _registry;
    private readonly Guid? _messageId;
    private readonly Guid? _correlationId;

    public ProtobufSerializerContext(
        object message,
        MessageContext context,
        string[] supportedMessageTypes,
        MessageBody? body,
        IProtobufMessageRegistry registry)
    {
        _message = message ?? throw new ArgumentNullException(nameof(message));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _supportedMessageTypes = supportedMessageTypes ?? throw new ArgumentNullException(nameof(supportedMessageTypes));
        _body = body;
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        _messageId = context.MessageId ?? TryGetGuid(context.Headers, MessageHeaders.MessageId, "MT-MessageId", "MessageId", "message-id", "message_id");
        _correlationId = context.CorrelationId ?? TryGetGuid(context.Headers, MessageHeaders.CorrelationId, "MT-CorrelationId", "CorrelationId", "correlation-id", "correlation_id");
    }

    public Guid? MessageId => _messageId;
    public Guid? RequestId => _context.RequestId;
    public Guid? CorrelationId => _correlationId;
    public Guid? ConversationId => _context.ConversationId;
    public Guid? InitiatorId => _context.InitiatorId;
    public DateTime? ExpirationTime => _context.ExpirationTime;
    public Uri? SourceAddress => _context.SourceAddress;
    public Uri? DestinationAddress => _context.DestinationAddress;
    public Uri? ResponseAddress => _context.ResponseAddress;
    public Uri? FaultAddress => _context.FaultAddress;
    public DateTime? SentTime => _context.SentTime;
    public Headers Headers => _context.Headers;
    public HostInfo Host => _context.Host;

    public string[] SupportedMessageTypes => _supportedMessageTypes;

    public bool IsSupportedMessageType<T>() where T : class
    {
        return typeof(T).IsAssignableFrom(_message.GetType());
    }

    public bool IsSupportedMessageType(Type messageType)
    {
        return messageType.IsAssignableFrom(_message.GetType());
    }

    public bool TryGetMessage<T>(out T message) where T : class
    {
        if (_message is T typed)
        {
            message = typed;
            return true;
        }

        message = default!;
        return false;
    }

    public bool TryGetMessage(Type messageType, out object message)
    {
        if (messageType.IsAssignableFrom(_message.GetType()))
        {
            message = _message;
            return true;
        }

        message = default!;
        return false;
    }

    public IMessageSerializer GetMessageSerializer()
    {
        if (_body != null)
        {
            return new CopyBodySerializer(ProtobufConstants.ProtobufContentType, _body);
        }

        return new ProtobufMessageSerializer(_registry);
    }

    public IMessageSerializer GetMessageSerializer<T>(MessageEnvelope envelope, T message) where T : class
    {
        return new ProtobufMessageSerializer(_registry);
    }

    public IMessageSerializer GetMessageSerializer(object message, string[] messageTypes)
    {
        return new ProtobufMessageSerializer(_registry);
    }

    public Dictionary<string, object> ToDictionary<T>(T? message) where T : class
    {
        if (message == null)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        var json = JsonSerializer.Serialize(message);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public T? DeserializeObject<T>(object? value, T? defaultValue = default) where T : class
    {
        return SystemTextJsonMessageSerializer.Instance.DeserializeObject(value, defaultValue);
    }

    public T? DeserializeObject<T>(object? value, T? defaultValue = default) where T : struct
    {
        return SystemTextJsonMessageSerializer.Instance.DeserializeObject(value, defaultValue);
    }

    public MessageBody SerializeObject(object? value)
    {
        return SystemTextJsonMessageSerializer.Instance.SerializeObject(value);
    }

    private static Guid? TryGetGuid(Headers headers, params string[] headerNames)
    {
        foreach (var name in headerNames)
        {
            var val = headers.Get<string>(name);
            if (!string.IsNullOrWhiteSpace(val) && Guid.TryParse(val, out var guid))
            {
                return guid;
            }
        }

        return null;
    }
}
