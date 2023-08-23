using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace client;

class WebSocketClient
{
    static async Task Start()
    {
        using (var client = new ClientWebSocket())
        {
            var uri = new Uri("ws://localhost:8080");
            await client.ConnectAsync(uri, CancellationToken.None);

            // Authenticate with the server
            var secretKey = "mysecretkey";
            var challenge = "mychallenge";
            var expected = secretKey + challenge;
            var authMessage = $"AUTH {expected}";
            var authBuffer = Encoding.UTF8.GetBytes(authMessage);
            await client.SendAsync(new ArraySegment<byte>(authBuffer), WebSocketMessageType.Text, true, CancellationToken.None);

            // Wait for the server to authenticate us
            var buffer = new byte[1024];
            while (true)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("Server disconnected");
                    break;
                }
                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (msg == "Client authenticated")
                {
                    Console.WriteLine("Server authenticated us");
                    break;
                }
            }

            // Send a message to the server
            var message = "Hello, server!";
            var messageBuffer = Encoding.UTF8.GetBytes(message);
            await client.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);

            // Wait for a response from the server
            buffer = new byte[1024];
            while (true)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("Server disconnected");
                    break;
                }
                var response = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"Received message from server: {response}");
            }
        }
    }
}