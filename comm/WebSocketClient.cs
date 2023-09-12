using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace comm;

public class WebSocketClient : IDisposable
{
    private bool _running;
    private bool _disposedValue;
    private CancellationTokenSource? _cts = new();
    private CancellationToken _ct;
    private ClientWebSocket? _client = new();

    public WebSocketClient()
    {
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
        var uri = new Uri("ws://localhost:8080");
        await _client.ConnectAsync(uri, _ct);

        var buffer = new byte[1024];
        var connectResult = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (connectResult.MessageType == WebSocketMessageType.Close)
        {
            Console.WriteLine("Server disconnected");
            _running = false;
            return;
        }
        var conMsg = Encoding.UTF8.GetString(buffer, 0, connectResult.Count);
        var challenge = "";
        if (conMsg?.StartsWith("AUTH ") == true)
        {
            challenge = conMsg[5..];
        }

        // Authenticate with the server
        const string secretKey = "mysecretkey";
        var expected = secretKey + challenge;
        var authMessage = $"AUTH {expected}";
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
        {
            throw new InvalidOperationException("WebSocket client is null.");
        }
        // Wait for a response from the server
        byte[] buffer = new byte[1024];
        var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            Console.WriteLine("Server disconnected");
            return "";
        }
        var response = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Console.WriteLine($"Received message from server: {response}");
        return response;
    }

    public async Task SendAsync(string message)
    {
        if (_client == null)
        {
            throw new InvalidOperationException("WebSocket client is null.");
        }
        var messageBuffer = Encoding.UTF8.GetBytes(message);
        await _client.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task<string> GetAsync(string message)
    {
        await SendAsync(message);
        return await ReceiveAsync();
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