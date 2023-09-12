public record Response(int StatusCode, KeyValuePair<string, string>[] Headers, string Body);
