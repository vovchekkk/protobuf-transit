using System.Net.Mime;

namespace ProtobufTransit;

public static class ProtobufConstants
{
    public const string ContentTypeString = "application/x-protobuf";
    public const string MessageTypeHeader = "MT-MessageType";
    public static readonly ContentType ProtobufContentType = new(ContentTypeString);
}
