using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using SensorSimulator.Dtos;
using SensorSimulator.Models;

namespace SensorSimulator.Services;

public class SensorConfigSyncWorker : BackgroundService
{
    private readonly ILogger<SensorConfigSyncWorker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISensorRuntimeRegistry _registry;
    private readonly IConfiguration _config;

    private readonly string _bootstrapServers;
    private readonly string _configTopic;

    public SensorConfigSyncWorker(
        ILogger<SensorConfigSyncWorker> logger,
        IHttpClientFactory httpClientFactory,
        ISensorRuntimeRegistry registry,
        IConfiguration config)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _registry = registry;
        _config = config;

        _bootstrapServers = config["Kafka:BootstrapServers"]
                            ?? config["Kafka__BootstrapServers"]
                            ?? "localhost:9092";

        _configTopic = config["Kafka:ConfigTopic"]
                       ?? config["Kafka__ConfigTopic"]
                       ?? "sensor-config-events";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var hostname = Environment.GetEnvironmentVariable("HOSTNAME")
                   ?? Environment.MachineName
                   ?? Guid.NewGuid().ToString("N");

    var groupId = $"sensorsim-{hostname}";

    var consumerConfig = new ConsumerConfig
    {
        BootstrapServers = _bootstrapServers,
        GroupId = groupId,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = true,
        SessionTimeoutMs = 10000
    };

    using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

    TimeSpan backoff = TimeSpan.FromSeconds(2);

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await InitialSyncAsync(stoppingToken);
            break;
        }
        catch
        {
            _logger.LogWarning("Initial sync failed. Retrying in {delay}s", backoff.TotalSeconds);
            await Task.Delay(backoff, stoppingToken);
            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
        }
    }

    backoff = TimeSpan.FromSeconds(2);

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            consumer.Subscribe(_configTopic);

            _logger.LogInformation(
                "SensorSimulator config consumer started. topic={topic} group={group}",
                _configTopic, groupId);

            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null)
                    continue;

                var ev = JsonSerializer.Deserialize<SensorConfigChangedEvent>(result.Message.Value);
                if (ev is null)
                    continue;

                await ApplyEventAsync(ev, stoppingToken);
                backoff = TimeSpan.FromSeconds(2); // reset on success
            }
        }
        catch (ConsumeException ex) when (
            ex.Error.Code == ErrorCode.UnknownTopicOrPart ||
            ex.Error.IsError)
        {
            _logger.LogWarning(
                ex,
                "Kafka config topic not ready. Retrying in {delay}s",
                backoff.TotalSeconds);
        }

        catch (KafkaException ex)
        {
            _logger.LogWarning(
                ex,
                "Kafka error (SensorSimulator). Retrying in {delay}s",
                backoff.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected SensorSimulator Kafka failure");
        }

        await Task.Delay(backoff, stoppingToken);
        backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
    }

    try { consumer.Close(); } catch { }
}


    private async Task InitialSyncAsync(CancellationToken ct)
{
    var client = _httpClientFactory.CreateClient("sensormanager");

    var response = await client.GetFromJsonAsync<SensorListResponse>(
        "/sensors?simulate=true&page=1&pageSize=500",
        ct);

    var items = response?.Items ?? new List<SensorDefinitionOut>();

    _logger.LogInformation("Initial sync loaded {count} sensors", items.Count);

    foreach (var s in items)
        await UpsertFromDefinitionAsync(s, ct);
}


    private async Task ApplyEventAsync(SensorConfigChangedEvent ev, CancellationToken ct)
    {
        if (ev.Action == "deleted")
        {
            await _registry.RemoveAsync(ev.SensorId, ct);
            return;
        }

        if (ev.Payload is null)
            return;

        if (!ev.Payload.Simulate || !ev.Payload.Enabled)
        {
            await _registry.RemoveAsync(ev.Payload.SensorId, ct);
            return;
        }

        await UpsertFromDefinitionAsync(ev.Payload, ct);
    }

    private Task UpsertFromDefinitionAsync(SensorDefinitionOut s, CancellationToken ct)
    {
        var cfg = new SensorConfig
        {
            SensorId = s.SensorId,
            SensorType = s.SensorType,
            IntervalMs = s.IntervalMs,
            MinValue = s.OperatingMin,
            MaxValue = s.OperatingMax,
            Enabled = s.Enabled
        };

        return _registry.UpsertAsync(cfg, ct);
    }

    private sealed class SensorListResponse
    {
        public List<SensorDefinitionOut> Items { get; set; } = new();
    }
}
