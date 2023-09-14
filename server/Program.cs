using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using comm;

namespace nexus;

public static class Program
{
    static async Task Main(string[] args)
    {
        // Load clients from json file. Deserialize using builtin .net
        // json serializer. If file does not exist, create it.
        if (!File.Exists("clients.json"))
        {
            throw new Exception("clients.json file not found");
        }

        var json = await File.ReadAllTextAsync("clients.json");
        var clients = JsonSerializer.Deserialize<List<Client>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new Exception("clients.json file is empty");

        // Create a WebSocket server
        using var listener = new HttpListener();
        listener.Prefixes.Add(Environment.GetEnvironmentVariable("NEXUS_URL") ?? "http://*:8080/");
        listener.Start();
        Console.WriteLine("Listening for WebSocket connections...");

        // Keep track of connected clients and their authentication status
        var wsClients = new List<WebSocketClient>();
        var tasks = new List<Task>();

        while (true)
        {
            // Wait for a new WebSocket connection
            var context = await listener.GetContextAsync();
            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            // Accept the WebSocket connection
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            Console.WriteLine("New client connected");

            // Send an authentication challenge to the client
            var challenge = Guid.NewGuid().ToString("N");
            var response = new Response(401, new Dictionary<string, string> { { "WWW-Authenticate", challenge } }, null);
            string responseText = response.ToJson();
            await webSocketContext.WebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(responseText)), WebSocketMessageType.Text, true, CancellationToken.None);

            // Listen for authentication responses from the client
            var buffer = new byte[1024];
            while (true)
            {
                try
                {
                    var result = await webSocketContext.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("Client disconnected");
                        break;
                    }

                    //TODO: Fix deserialization bug that throws exception when deserializing.
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Request request = message.ToRequest();

                    if (request.Method.Equals(RequestMethod.AUTH) && request.Body.Any())
                    {
                        WebSocketClient cli = request.Body.ToWebSocketClient(webSocketContext.WebSocket);
                        var authResponse = cli.Secret;
                        var expected = clients.Find(x => x.Name == cli.Name)?.Secret + challenge;
                        if (authResponse == expected)
                        {
                            Console.WriteLine("Client authenticated");
                            var authenticatedResponse = new Response(200, new Dictionary<string, string>(), null).ToJsonBuffer();
                            await cli.WebSocket.SendAsync(new ArraySegment<byte>(authenticatedResponse), WebSocketMessageType.Text, true, CancellationToken.None);
                            wsClients.Add(cli);
                            tasks.Add(StartForwarding(cli, wsClients));
                        }
                        else
                        {
                            Console.WriteLine("Client authentication failed");
                            await webSocketContext.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client authentication failed", CancellationToken.None);
                        }
                    }

                }
                catch (Exception)
                {
                    Console.WriteLine("Client disconnected");
                    break;
                }
            }
        }
    }

    // Start forwarding messages between the two authenticated clients
    static async Task StartForwarding(WebSocketClient sourceClient, List<WebSocketClient> otherClients)
    {
        Console.WriteLine("Starting message forwarding");
        var buffer = new byte[1024];
        while (true)
        {
            var result = await sourceClient.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine("Client disconnected");
                break;
            }
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Message msg = message.ToMessage();
            if (msg.MessageType.Equals(MessageType.Request))
            {
                Request request = message.ToRequest();
                if (request.Method.Equals(RequestMethod.LIST))
                {
                    var clients = otherClients.Select(x => x.Client).ToList();
                    var response = new Response(200, new Dictionary<string, string>(), clients.ToJson()).ToJsonBuffer();
                    await sourceClient.WebSocket.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                    continue;
                }
                if (request.Method.Equals(RequestMethod.GET))
                {
                    var targetClient = otherClients.Find(x => x.Client.Name == request.Target);
                    if (targetClient == null)
                    {
                        var notFoundResponse = new Response(404, new Dictionary<string, string>(), null).ToJsonBuffer();
                        await sourceClient.WebSocket.SendAsync(new ArraySegment<byte>(notFoundResponse), WebSocketMessageType.Text, true, CancellationToken.None);
                        continue;
                    }
                    var forwardRequest = request.ToJsonBuffer();
                    await targetClient.WebSocket.SendAsync(new ArraySegment<byte>(forwardRequest), WebSocketMessageType.Text, true, CancellationToken.None);
                    var targetWebSocketResult = await targetClient.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (targetWebSocketResult.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("Target Client disconnected");
                        var notFoundResponse = new Response(404, new Dictionary<string, string>(), null).ToJsonBuffer();
                        await sourceClient.WebSocket.SendAsync(new ArraySegment<byte>(notFoundResponse), WebSocketMessageType.Text, true, CancellationToken.None);
                        continue;
                    }
                    var targetResult = Encoding.UTF8.GetString(buffer, 0, targetWebSocketResult.Count);
                    Message targetMessage = targetResult.ToMessage();
                    if (targetMessage.MessageType.Equals(MessageType.Response))
                    {
                        Response response = targetResult.ToResponse();
                        var forwardResponse = response.ToJsonBuffer();
                        await sourceClient.WebSocket.SendAsync(new ArraySegment<byte>(forwardResponse), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    continue;
                }
            }
        }
    }
}
