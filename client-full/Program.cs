using comm;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", false, true);
builder.Configuration.AddEnvironmentVariables();
var config = builder.Configuration;

var app = builder.Build();
var nexusUrl = config["NEXUS_URL"] ?? "";
var nexusSecret = config["NEXUS_SECRET"] ?? "";
var clientName = config["CLIENT_NAME"] ?? "";

var webSocketClient = new NexusClient(clientName, nexusUrl, nexusSecret);
await webSocketClient.StartAsync();

app.MapGet("/{**catchAll}", async (HttpContext context) =>
{
    var queryString = string.Join(", ", context.Request.Query.Select(kv => $"{kv.Key}: {kv.Value}"));
    var request = new Request(RequestMethod.GET, "", null, null, null);
    var result = await webSocketClient.GetAsync(request);
    return result;
});

app.Run();