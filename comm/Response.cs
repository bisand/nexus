public record Response(int StatusCode, IDictionary<string, string> Headers, string? Body);
