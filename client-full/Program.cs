using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using comm;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var webSocketClient = new WebSocketClient();
Task task = webSocketClient.Start();

app.MapGet("/{**catchAll}", async (HttpContext context) =>
{
    var queryString = string.Join(", ", context.Request.Query.Select(kv => $"{kv.Key}: {kv.Value}"));
    // await context.Response.WriteAsync($"Hello World! Path: {context.Request.Path}, Query: {queryString}");
    while(!task.IsCompleted)
    {
        Thread.Sleep(100);
    }
    var result = await webSocketClient.GetAsync("GET http://vg.no");
    return result;
});

app.Run();