using System.Net.WebSockets;

namespace comm;

public record Client(string Name, ClientTypes Type, string Secret);

public record WebSocketClient(WebSocket WebSocket, Client Client) : Client(Client.Name, Client.Type, Client.Secret);
