using System.IO;
using MassTransit;

namespace ProtobufTransit;

internal sealed class ProtobufMessageBody : MessageBody
{
    private readonly byte[] _bytes;

    public ProtobufMessageBody(byte[] bytes)
    {
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
    }

    public long? Length => _bytes.Length;

    public byte[] GetBytes() => _bytes;

    public string GetString() => Convert.ToBase64String(_bytes);

    public Stream GetStream() => new MemoryStream(_bytes, writable: false);
}
