namespace SensorSimulator.Dtos;

public record MeasurementOut(
    string SensorId,
    DateTimeOffset Timestamp,
    double Value
);
