# SensorSimulator

Simulation service.

Keeps an up-to-date view of sensor configuration and generates measurements for sensors that are `enabled` and `simulate=true`.

- initial sync: SensorManager REST
- updates: Kafka `sensor-config-events`
- output: POST to Archiver + Kafka topic `measurements`

## Branching

- `dev` – development
- `main` – stable / demo-ready

## Requirements

- .NET SDK (tested with 10.0.101)
- Kafka
- Access to SensorManager + Archiver

## Run

Recommended: run the full stack with Docker Compose (see `infra/docker`).

Local run:

```bash
dotnet run
```

## Configuration

Environment variables used in Docker/Kubernetes:

- `SensorManager__BaseUrl` – SensorManager base URL
- `ArchiverUrl` (or `Archiver__BaseUrl`) – Archiver base URL
- `Kafka__BootstrapServers` – Kafka bootstrap servers

## API

Swagger (docker default): `http://localhost:8084/swagger`

Endpoints:

- `GET /simulated-sensors`
- `POST /simulated-sensors`
- `PUT /simulated-sensors/{sensorId}`
- `DELETE /simulated-sensors/{sensorId}`

Health:

- `GET /health/live`
- `GET /health/ready`
