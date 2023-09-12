using comm;

var webSocketClient = new WebSocketClient();
await webSocketClient.StartAsync();

while (true)
{
    var message = await webSocketClient.ReceiveAsync();
    if (message.StartsWith("GET"))
    {
        var url = message[3..].Trim();
        var client = new HttpClient();
        var response = await client.GetStringAsync(url);
        await webSocketClient.SendAsync(response);
    }
    else
    {
        Console.WriteLine($"Received unexpected message from client: {message}");
    }
}
