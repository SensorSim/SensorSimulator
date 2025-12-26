# SensorSimulator

**Purpose:** Keeps an up-to-date view of sensor configuration and produces measurements.

It does **not** have its own database. It reads configuration via:
- SensorManager REST (initial sync)
- Kafka `sensor-config-events` (updates)

Then it simulates enabled sensors and:
- writes measurements to **Archiver** via REST
- publishes a live stream to Kafka topic **`measurements`**

---

## API

Swagger:
- `http://localhost:8084/swagger`

Endpoints:
- `GET /simulated-sensors` (view/drive simulation state)
- `POST /simulated-sensors`
- `PUT /simulated-sensors/{sensorId}`
- `DELETE /simulated-sensors/{sensorId}`
- `GET /health/live`
- `GET /health/ready`

Note: the simulated-sensors endpoints act as a thin “simulation control” layer and typically delegate config updates back to SensorManager.

---

## Configuration (env vars)

- `SensorManager__BaseUrl` (or equivalent) – where to reach SensorManager
- `Archiver__BaseUrl` (or equivalent) – where to send measurements
- Kafka bootstrap settings (see compose env)
