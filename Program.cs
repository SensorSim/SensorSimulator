using SensorSimulator.Services;
using SensorSimulator.Dtos;
using Confluent.Kafka;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("archiver", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ArchiverUrl"]
        ?? "http://localhost:8081");
});

builder.Services.AddHttpClient("sensormanager", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["SensorManagerUrl"]
        ?? "http://localhost:8083");
});
builder.Services.AddSingleton<ISensorRuntimeRegistry, SensorRuntimeRegistry>();
builder.Services.AddHostedService<SensorConfigSyncWorker>();

// Kafka producer (for real-time stream). REST -> Archiver remains unchanged.
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
                           ?? builder.Configuration["Kafka__BootstrapServers"]
                           ?? "localhost:9092";

    var config = new ProducerConfig
    {
        BootstrapServers = bootstrapServers,
        // Helps avoid duplicating messages during transient retries.
        EnableIdempotence = true
    };

    return new ProducerBuilder<string, string>(config).Build();
});

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "sensor-simulator",
    status = "running"
}));

// CRUD endpoints (proxy to SensorManager)
app.MapGet("/simulated-sensors", (ISensorRuntimeRegistry registry) => Results.Ok(registry.List()));

app.MapPost("/simulated-sensors", async (SensorDefinitionIn input, IHttpClientFactory f, CancellationToken ct) =>
{
    var client = f.CreateClient("sensormanager");
    var res = await client.PostAsJsonAsync("/sensors", input with { Simulate = true }, ct);
    var body = await res.Content.ReadFromJsonAsync<SensorDefinitionOut>(cancellationToken: ct);
    return res.IsSuccessStatusCode
        ? Results.Created($"/simulated-sensors/{body?.SensorId}", body)
        : Results.StatusCode((int)res.StatusCode);
});

app.MapPut("/simulated-sensors/{sensorId}", async (string sensorId, SensorDefinitionIn input, IHttpClientFactory f, CancellationToken ct) =>
{
    var client = f.CreateClient("sensormanager");

    // Find by SensorId -> Id
    var list = await client.GetFromJsonAsync<SensorListResponse>("/sensors?page=1&pageSize=500", ct);
    var match = list?.Items?.FirstOrDefault(x => x.SensorId == sensorId);
    if (match is null) return Results.NotFound();

    var res = await client.PutAsJsonAsync($"/sensors/{match.Id}", input with { Simulate = true }, ct);
    var body = await res.Content.ReadFromJsonAsync<SensorDefinitionOut>(cancellationToken: ct);
    return res.IsSuccessStatusCode ? Results.Ok(body) : Results.StatusCode((int)res.StatusCode);
});

app.MapDelete("/simulated-sensors/{sensorId}", async (string sensorId, IHttpClientFactory f, CancellationToken ct) =>
{
    var client = f.CreateClient("sensormanager");
    var list = await client.GetFromJsonAsync<SensorListResponse>("/sensors?page=1&pageSize=500", ct);
    var match = list?.Items?.FirstOrDefault(x => x.SensorId == sensorId);
    if (match is null) return Results.NotFound();

    var res = await client.DeleteAsync($"/sensors/{match.Id}", ct);
    return res.IsSuccessStatusCode ? Results.NoContent() : Results.StatusCode((int)res.StatusCode);
});

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.Run();

internal sealed class SensorListResponse
{
    public List<SensorDefinitionOut> Items { get; set; } = new();
}
