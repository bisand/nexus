public record Request(string Method, string Path, KeyValuePair<string, string>[] Headers, string Body);
