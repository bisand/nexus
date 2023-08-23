var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/{**catchAll}", (string catchAll) =>
{
    return $"Hello World! {catchAll}";
});

app.Run();
