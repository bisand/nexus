namespace comm;
public record Message(MessageType MessageType, IDictionary<string, string>? Headers, string? Body);
