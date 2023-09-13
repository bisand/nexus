using System.Net.WebSockets;
using System.Text;

namespace comm;

public class NexusClient : IDisposable
{
    private bool _running;
    private bool _disposedValue;
    private CancellationTokenSource? _cts = new();
    private CancellationToken _ct;
    private ClientWebSocket? _client = new();
    private readonly Uri _nexusUri;
    private readonly string _clientName;
    private readonly string _nexusSecret;

    public NexusClient(string clientName, string nexusUrl, string nexusSecret)
    {
        if (string.IsNullOrWhiteSpace(nexusUrl))
            throw new ArgumentException("Nexus URL cannot be null or empty", nameof(nexusUrl));
        if (string.IsNullOrWhiteSpace(clientName))
            throw new ArgumentException("Client name cannot be null or empty", nameof(clientName));
        if (string.IsNullOrWhiteSpace(nexusSecret))
            throw new ArgumentException("Nexus secret cannot be null or empty", nameof(nexusSecret));

        _nexusUri = new Uri(nexusUrl);
        _clientName = clientName;
        _nexusSecret = nexusSecret;
    }

    public async Task StartAsync(CancellationToken? ct = null)
    {
        if (_client == null)
        {
            throw new InvalidOperationException("WebSocket client is null.");
        }
        if (_running)
        {
            return;
        }
        _running = true;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct ?? CancellationToken.None);
        _ct = _cts.Token;
        await _client.ConnectAsync(_nexusUri, _ct);

        var buffer = new byte[1024];
        var connectResult = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (connectResult.MessageType == WebSocketMessageType.Close)
        {
            Console.WriteLine("Server disconnected");
            _running = false;
            return;
        }
        var conMsg = Encoding.UTF8.GetString(buffer, 0, connectResult.Count);
        Message message = conMsg.ToMessage();
        if (!message.MessageType.Equals(MessageType.Response))
        {
            Console.WriteLine($"Received unexpected message from server: {conMsg}");
            _running = false;
            return;
        }
        Response response = conMsg.ToResponse();
        var challenge = "";
        if (response.StatusCode == 401 && response.Headers.TryGetValue("WWW-Authenticate", out var authHeader))
        {
            challenge = authHeader;
        }

        // Authenticate with the server
        const string secretKey = "mysecretkey";
        var expected = secretKey + challenge;
        var authMessage = $"AUTH {expected}";
        var request = new Request(RequestMethod.AUTH, null, _nexusUri, null, new Client(_clientName, ClientTypes.Client, _nexusSecret).ToJson());
        var authBuffer = Encoding.UTF8.GetBytes(authMessage);
        await _client.SendAsync(new ArraySegment<byte>(authBuffer), WebSocketMessageType.Text, true, CancellationToken.None);

        // Wait for the server to authenticate us
        while (true)
        {
            var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine("Server disconnected");
                _running = false;
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
        // await SendAsync("Hello from the client");
    }

    public async Task<string> ReceiveAsync()
    {
        if (_client == null)
            throw new InvalidOperationException("WebSocket client is null.");

        // Wait for a response from the server
        byte[] buffer = new byte[1024];
        var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            Console.WriteLine("Server disconnected");
            return new Response(200, null, "Server disconnected").ToJson();
        }
        var response = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Console.WriteLine($"Received message from server: {response}");
        return response;
    }

    public async Task SendAsync(Request message)
    {
        if (_client == null)
            throw new InvalidOperationException("WebSocket client is null.");
        await _client.SendAsync(new ArraySegment<byte>(message.ToJsonBuffer()), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task SendAsync(Response message)
    {
        if (_client == null)
            throw new InvalidOperationException("WebSocket client is null.");
        await _client.SendAsync(new ArraySegment<byte>(message.ToJsonBuffer()), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task<Response> GetAsync(Request message)
    {
        await SendAsync(message);
        return (await ReceiveAsync()).ToResponse();
    }

    public async Task Stop()
    {
        if (!_running)
        {
            return;
        }
        _running = false;
        if (_client == null)
        {
            return;
        }
        await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client stopped", _ct);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _cts?.Cancel();
                _client?.Dispose();
                _cts?.Dispose();
            }

            _client = null;
            _cts = null;
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}