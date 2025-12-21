using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using SensorSimulator.Dtos;
using SensorSimulator.Models;

namespace SensorSimulator.Services;

public interface ISensorRuntimeRegistry
{
    IReadOnlyCollection<SensorConfig> List();
    Task UpsertAsync(SensorConfig config, CancellationToken ct);
    Task RemoveAsync(string sensorId, CancellationToken ct);
}

public class SensorRuntimeRegistry : ISensorRuntimeRegistry
{
    private readonly ConcurrentDictionary<string, (SensorConfig Config, CancellationTokenSource Cts, Task Task)> _running = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IProducer<string, string> _kafkaProducer;
    private readonly ILogger<SensorRuntimeRegistry> _logger;
    private readonly string _kafkaTopic;

    public SensorRuntimeRegistry(
        IHttpClientFactory httpClientFactory,
        IProducer<string, string> kafkaProducer,
        IConfiguration configuration,
        ILogger<SensorRuntimeRegistry> logger)
    {
        _httpClientFactory = httpClientFactory;
        _kafkaProducer = kafkaProducer;
        _logger = logger;

        _kafkaTopic = configuration["Kafka:Topic"]
                      ?? configuration["Kafka__Topic"]
                      ?? "measurements";
    }

    public IReadOnlyCollection<SensorConfig> List() => _running.Values.Select(v => v.Config).ToList();

    public async Task UpsertAsync(SensorConfig config, CancellationToken ct)
    {
        // If exists, remove first then start a new task with updated settings.
        if (_running.TryGetValue(config.SensorId, out _))
            await RemoveAsync(config.SensorId, ct);

        if (!config.Enabled)
        {
            _logger.LogInformation("Sensor {id} not started because Enabled=false", config.SensorId);
            return;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var task = RunSensorAsync(config, cts.Token);

        _running[config.SensorId] = (config, cts, task);

        _logger.LogInformation("Sensor {id} started intervalMs={interval}", config.SensorId, config.IntervalMs);
    }

    public async Task RemoveAsync(string sensorId, CancellationToken ct)
    {
        if (_running.TryRemove(sensorId, out var existing))
        {
            try
            {
                existing.Cts.Cancel();
            }
            catch { }

            try
            {
                await Task.WhenAny(existing.Task, Task.Delay(2000, ct));
            }
            catch { }

            _logger.LogInformation("Sensor {id} stopped", sensorId);
        }
    }

    private async Task RunSensorAsync(SensorConfig sensor, CancellationToken ct)
    {
        var rnd = new Random();
        var client = _httpClientFactory.CreateClient("archiver");

        while (!ct.IsCancellationRequested)
        {
            var value = sensor.MinValue + rnd.NextDouble() * (sensor.MaxValue - sensor.MinValue);

            var measurement = new MeasurementOut(
                sensor.SensorId,
                DateTimeOffset.UtcNow,
                Math.Round(value, 2)
            );

            try
            {
                await client.PostAsJsonAsync("/measurements", measurement, ct);

                var json = JsonSerializer.Serialize(measurement);
                await _kafkaProducer.ProduceAsync(
                    _kafkaTopic,
                    new Message<string, string>
                    {
                        Key = sensor.SensorId,
                        Value = json
                    },
                    ct);

                _logger.LogDebug("Sensor {id} -> {value}", sensor.SensorId, measurement.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending measurement for sensor {id}", sensor.SensorId);
            }

            try
            {
                await Task.Delay(sensor.IntervalMs, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
