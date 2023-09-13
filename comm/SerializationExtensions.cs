using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace comm;
public static class SerializationExtensions
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions DeSerializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static Message ToMessage(this string json) => JsonSerializer.Deserialize<Message>(json, DeSerializeOptions) ?? throw new Exception("Request error");
    public static Request ToRequest(this string json) => JsonSerializer.Deserialize<Request>(json, DeSerializeOptions) ?? throw new Exception("Request error");
    public static Response ToResponse(this string json) => JsonSerializer.Deserialize<Response>(json, DeSerializeOptions) ?? throw new Exception("Response error");
    public static Client ToClient(this string json) => JsonSerializer.Deserialize<Client>(json, DeSerializeOptions) ?? throw new Exception("Client error");
    public static WebSocketClient ToWebSocketClient(this string json, WebSocket webSocket) => new WebSocketClient(webSocket, JsonSerializer.Deserialize<Client>(json, DeSerializeOptions) ?? throw new Exception("Client error"));
    public static string ToJson(this Response response) => JsonSerializer.Serialize(response, SerializeOptions);
    public static byte[] ToJsonBuffer(this Request request) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, SerializeOptions));
    public static byte[] ToJsonBuffer(this Response response) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response, SerializeOptions));
    public static string ToJson(this Request request) => JsonSerializer.Serialize(request, SerializeOptions);
    public static string ToJson(this Client client) => JsonSerializer.Serialize(client, SerializeOptions);
    public static string ToJson(this IEnumerable<Client> clients) => JsonSerializer.Serialize(clients, SerializeOptions);

}