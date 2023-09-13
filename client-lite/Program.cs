using comm;

var webSocketClient = new NexusClient("", "", "");
await webSocketClient.StartAsync();

while (true)
{
    var result = await webSocketClient.ReceiveAsync();
    Message message = result.ToMessage();
    if (message.MessageType.Equals(MessageType.Request))
    {
        var requestMessage = result.ToRequest();
        var uri = requestMessage.Uri;
        var client = new HttpClient();
        var webResponse = await client.GetStringAsync(uri);
        await webSocketClient.SendAsync(webResponse.ToResponse());
    }
    else if (message.MessageType.Equals(MessageType.Response))
    {
        Console.WriteLine($"Received response message from client: {message}");
    }
    else
    {
        Console.WriteLine($"Received unexpected message from client: {message}");
    }
}
