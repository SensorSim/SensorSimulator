namespace SensorSimulator.Models;

public class SensorConfig
{
    public string SensorId { get; init; } = default!;
    public string SensorType { get; init; } = default!;
    public int IntervalMs { get; init; }
    public double MinValue { get; init; }
    public double MaxValue { get; init; }
    public bool Enabled { get; set; } = true;
}
