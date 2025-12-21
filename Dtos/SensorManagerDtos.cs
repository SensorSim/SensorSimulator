namespace SensorSimulator.Dtos;

public record SensorDefinitionIn(
    string SensorId,
    string SensorType,
    string Unit,
    double OperatingMin,
    double OperatingMax,
    double WarningMin,
    double WarningMax,
    int IntervalMs,
    bool Enabled,
    bool Simulate);

public record SensorDefinitionOut(
    Guid Id,
    string SensorId,
    string SensorType,
    string Unit,
    double OperatingMin,
    double OperatingMax,
    double WarningMin,
    double WarningMax,
    int IntervalMs,
    bool Enabled,
    bool Simulate,
    DateTimeOffset UpdatedAt);

public record SensorConfigChangedEvent(
    string Action,
    string SensorId,
    DateTimeOffset Timestamp,
    SensorDefinitionOut? Payload);
