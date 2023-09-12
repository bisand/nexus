using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace nexus;

public static class Program
{
    static async Task Main(string[] args)
    {
        // Create a WebSocket server
        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");
        listener.Start();
        Console.WriteLine("Listening for WebSocket connections...");

        // Keep track of connected clients and their authentication status
        var clients = new Dictionary<WebSocket, bool>();

        // Shared secret key for authentication
        const string secretKey = "mysecretkey";

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
            var webSocket = await context.AcceptWebSocketAsync(null);
            Console.WriteLine("New client connected");

            // Send an authentication challenge to the client
            var challenge = Guid.NewGuid().ToString("N");
            clients[webSocket.WebSocket] = false;
            await webSocket.WebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"AUTH {challenge}")), WebSocketMessageType.Text, true, CancellationToken.None);

            // Listen for authentication responses from the client
            var buffer = new byte[1024];
            while (true)
            {
                var result = await webSocket.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("Client disconnected");
                    await webSocket.WebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"Client disconnected")), WebSocketMessageType.Text, true, CancellationToken.None);
                    break;
                }
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (message.StartsWith("AUTH "))
                {
                    var response = message.Substring(5);
                    var expected = secretKey + challenge;
                    if (response == expected)
                    {
                        Console.WriteLine("Client authenticated");
                        await webSocket.WebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"Client authenticated")), WebSocketMessageType.Text, true, CancellationToken.None);
                        clients[webSocket.WebSocket] = true;

                        //TODO: Accept all authenticated clients, not just the first two and route messages between them based on id
                        if (clients.Count == 2 && clients.Values.All(authenticated => authenticated))
                        {
                            await StartForwarding(clients.Keys);
                        }
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Client authentication failed");
                        await webSocket.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client authentication failed", CancellationToken.None);
                        break;
                    }
                }
            }
        }
    }

    // Start forwarding messages between the two authenticated clients
    static async Task StartForwarding(IEnumerable<WebSocket> clients)
    {
        Console.WriteLine("Starting message forwarding");
        var clientList = clients.ToList();
        var buffer = new byte[1024];
        while (true)
        {
            var result = await clientList[0].ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine("Client disconnected");
                break;
            }
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Received message from client1: {message}");
            await clientList[1].SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
        }
    }
}