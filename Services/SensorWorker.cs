using System.Net.Http.Json;
using SensorSimulator.Dtos;
using SensorSimulator.Models;

namespace SensorSimulator.Services;

public class SensorWorker : BackgroundService
{
    private readonly ILogger<SensorWorker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly List<SensorConfig> _sensors =
    [
        new()
        {
            SensorId = "temp-1",
            SensorType = "temperature",
            IntervalMs = 2000,
            MinValue = 50,
            MaxValue = 150
        },
        new()
        {
            SensorId = "pressure-1",
            SensorType = "pressure",
            IntervalMs = 3000,
            MinValue = 1,
            MaxValue = 5
        }
    ];

    public SensorWorker(
        ILogger<SensorWorker> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = _sensors
            .Where(s => s.Enabled)
            .Select(s => RunSensorAsync(s, stoppingToken));

        await Task.WhenAll(tasks);
    }

    private async Task RunSensorAsync(SensorConfig sensor, CancellationToken ct)
    {
        var rnd = new Random();
        var client = _httpClientFactory.CreateClient("archiver");

        while (!ct.IsCancellationRequested)
        {
            var value = sensor.MinValue +
                        rnd.NextDouble() * (sensor.MaxValue - sensor.MinValue);

            var measurement = new MeasurementOut(
                sensor.SensorId,
                DateTimeOffset.UtcNow,
                Math.Round(value, 2)
            );

            try
            {
                await client.PostAsJsonAsync("/measurements", measurement, ct);
                _logger.LogInformation(
                    "Sensor {id} -> {value}",
                    sensor.SensorId,
                    measurement.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending measurement");
            }

            await Task.Delay(sensor.IntervalMs, ct);
        }
    }
}
