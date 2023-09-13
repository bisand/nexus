namespace comm;
public record Response(int StatusCode, IDictionary<string, string>? Headers, string? Body) : Message(MessageType.Response, Headers, Body);
