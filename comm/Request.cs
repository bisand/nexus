public record Request(string Method, string Target, Uri Uri, KeyValuePair<string, string>[] Headers, string Body) : Message("Request");
