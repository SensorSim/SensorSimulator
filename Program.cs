using SensorSimulator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("archiver", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ArchiverUrl"]
        ?? "http://localhost:8081");
});

builder.Services.AddHostedService<SensorWorker>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "sensor-simulator",
    status = "running"
}));

app.Run();
