namespace comm;
public record Request(RequestMethod Method, string? Target, Uri? Uri, IDictionary<string, string> Headers, string? Body) : Message(MessageType.Request, Headers, Body);
