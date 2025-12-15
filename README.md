# Sensor Simulator Service

Sensor Simulator is a microservice responsible for generating synthetic sensor data
and sending it to the Archiver service via REST API.

It simulates multiple sensor instances and types and is designed to be horizontally
scalable and configuration-driven.

---

## Responsibilities
- Periodically generate sensor measurements
- Support multiple sensor instances and sensor types
- Send measurements to Archiver service
- Run as a background worker (no external API required)

---

## Tech Stack
- .NET (ASP.NET Core Minimal API)
- BackgroundService
- REST (HttpClient)
- Docker
- GitHub Actions (CI)

---

## Architecture Role
Sensor Simulator acts as a **producer** in the system architecture.
It does not persist data and does not contain business logic such as alarms.
It communicates exclusively via REST APIs.

---

## Data Format

Measurements sent to Archiver:

```json
{
  "sensorId": "temp-1",
  "timestamp": "2025-12-15T20:15:00Z",
  "value": 123.45
}
```

---

## Configuration

### Environment Variables

| Variable | Description | Default |
|---------|-------------|---------|
| ArchiverUrl | Base URL of Archiver service | http://localhost:8081 |

Example:
```bash
ArchiverUrl=http://archiver:8080
```

---

## Local Development

### Prerequisites
- .NET SDK
- Docker (optional)

### Run locally
```bash
dotnet run
```

The service starts immediately and begins sending measurements.

---

## Docker

### Build image
```bash
docker build -t sensorsimulator:local .
```

### Run container
```bash
docker run --rm \
  -e ArchiverUrl=http://archiver:8080 \
  sensorsimulator:local
```

---

## Docker Compose Integration

Example:
```yaml
sensor-simulator:
  image: sensorsimulator:local
  environment:
    ArchiverUrl: http://archiver:8080
```

---

## Scaling
The service is stateless and can be horizontally scaled.
Multiple instances can run in parallel and send data to the same Archiver service.

---

## CI/CD
The repository includes GitHub Actions pipelines for:
- .NET build
- Docker image build
