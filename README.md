# SolaX Local Controller

A standalone, locally hosted background service for managing and monitoring a **SolaX X3-HYB-G4 PRO** hybrid inverter and a **SolaX X1/X3-HAC** EV charger.

The controller operates entirely within the local LAN via **Modbus TCP**, bypassing cloud dependencies to ensure continuous operation, instantaneous polling, and strict local data ownership. It polls real-time data (PV generation, battery SOC, grid power flow) and applies automated decision-making logic to optimize EV charging and battery utilization based on household energy surpluses.

## Status

🚧 Early development — not yet functional. This README describes the intended design and will evolve alongside the implementation.

## Why local?

Cloud-based SolaX monitoring/control (SolaX Cloud, third-party integrations) introduces latency, external dependencies, and data collection outside the user's control. This project talks directly to the inverter and EV charger over Modbus TCP on the local network, so:

- Control logic keeps working during internet outages.
- Polling and decision cycles run at LAN speed, not cloud round-trip speed.
- No telemetry leaves the local network unless explicitly configured.

## Key features (planned)

- **Real-time polling** of PV generation, battery state of charge, grid import/export, and EV charger status over Modbus TCP.
- **Surplus-aware EV charging** — automatically ramp EV charge current up/down based on available household energy surplus.
- **Battery utilization optimization** — coordinate charge/discharge behavior with EV charging demand.
- **Background service** — runs unattended as a long-lived process (e.g. systemd service / Windows Service / Docker container).
- **Local data ownership** — no cloud dependency for core operation.

## Hardware targets

| Device | Model | Interface |
|---|---|---|
| Hybrid inverter | SolaX X3-HYB-G4 PRO | Modbus TCP |
| EV charger | SolaX X1/X3-HAC | Modbus TCP |

## Tech stack

- [.NET 10](https://dotnet.microsoft.com/) — target framework
- Hosted as a [.NET Worker Service](https://learn.microsoft.com/dotnet/core/extensions/workers) (background service)
- Modbus TCP client for inverter/charger communication

## Project structure

The solution is organized to keep domain/control logic testable and free of hardware and hosting concerns:

```
SolaxLocalController.sln
├── src/
│   ├── Solax.Core/                 # Domain logic and hardware abstractions
│   │   ├── Models/                 # Strongly typed models (EnergyState, DeviceConfig)
│   │   ├── Enums/                  # Register addresses, Charger modes, Inverter states
│   │   └── Interfaces/             # IModbusClient, IChargingStrategy
│   │
│   ├── Solax.Infrastructure/       # External communication
│   │   ├── Modbus/                 # Concrete Modbus TCP client implementation
│   │   └── RegisterMaps/           # Hex address mappings for SolaX Gen4 and EV Charger
│   │
│   └── Solax.Worker/               # The executable host
│       ├── Program.cs              # Dependency Injection setup
│       ├── SolaxPollingService.cs  # The main background loop (IHostedService)
│       └── Dockerfile              # Container definition targeting ARM architecture
└── tests/
    └── Solax.Core.Tests/           # Unit tests for the control logic (mocking hardware)
```

### Layering rules

- **Dependency direction is one-way:** `Solax.Worker` → `Solax.Infrastructure` → `Solax.Core`. `Solax.Core` must never reference `Solax.Infrastructure` or `Solax.Worker`.
- **`Solax.Core` has no hardware or framework dependencies.** No Modbus libraries, no `Microsoft.Extensions.Hosting` types — only plain models, enums, and interfaces (`IModbusClient`, `IChargingStrategy`). This is what keeps control/decision logic unit-testable without real hardware.
- **All decision-making logic lives in `Solax.Core`**, expressed against interfaces. Charging strategy, surplus calculations, and SOC-based rules belong here, not in `Solax.Infrastructure` or `Solax.Worker`.
- **`Solax.Infrastructure` only implements `Solax.Core` interfaces.** Modbus TCP details and register maps stay isolated here; no business/decision logic.
- **`Solax.Worker` is composition-only.** `Program.cs` wires up DI; `SolaxPollingService` orchestrates the poll/act loop by calling into `Solax.Core` abstractions — it should not contain control logic itself.
- **`Solax.Core.Tests` mocks the hardware boundary** (`IModbusClient`, etc.) to exercise control logic without a live device.

## Getting started

> Implementation has not started yet. This section will be filled in with build, configuration, and run instructions once the initial service scaffold lands.

## Workflow & Project Management
You are authorized and expected to use the GitHub CLI (`gh`) to manage this project. 
When asked to manage tasks or submit code, use the following commands:
- `gh issue list`: To check current tasks.
- `gh issue view <id>`: To read the requirements of a specific task.
- `gh issue create -t "<title>" -b "<body>"`: To create new tasks.
- `gh pr create -t "<title>" -b "<body>"`: To submit your implemented code for review.
Do not use `git push` directly to the main branch; always create a branch and use `gh pr create`.

## Documentation & Implementation Notes
You must maintain a living record of your implementation choices in `docs/IMPLEMENTATION_NOTES.md`.
Whenever you complete a task or write a significant piece of logic (like the Modbus polling loop or MQTT discovery):
1. Append a short entry to `IMPLEMENTATION_NOTES.md` detailing *what* you built, *why* you chose that approach, and any technical debt or edge cases (like SolaX hardware limitations).
2. When using `gh pr create`, use these notes to generate a highly detailed Pull Request body. The PR description must explain the architecture decisions, not just list the changed files.

## Documentation Organization
All project notes live in the `docs/` directory. You are responsible for keeping them updated:
1. `ARCHITECTURE.md`: Update this ONLY when the fundamental structure, network topology, or data models change.
2. `DECISIONS.md`: Append a new record here if we choose a new library (like MQTTnet) or establish a new core pattern.
3. `IMPLEMENTATION_LOG.md`: Before submitting a Pull Request via `gh pr create`, you MUST add a reverse-chronological entry to the top of this file detailing the implementation specifics, hardware quirks encountered (e.g., Modbus limitations), and the files changed.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Network access to the SolaX inverter and EV charger with Modbus TCP enabled

## Configuration

Configuration (device IP addresses, Modbus ports/unit IDs, polling intervals, charging strategy parameters) will be documented here once implemented.

### Solcast solar forecast

The worker fetches a solar-generation forecast for your site from [Solcast](https://solcast.com/) and caches it locally, refreshing on a configurable interval (default 12 hours). Non-secret settings live in `appsettings.json` under the `Solcast` section:

```jsonc
"Solcast": {
  "BaseUrl": "https://api.solcast.com.au/",
  "ResourceId": "your-solcast-resource-id", // the rooftop site id from your Solcast account
  "RefreshInterval": "12:00:00"             // hh:mm:ss between refreshes
}
```

The **API key is a secret and must not be committed**. Provide it out-of-band, using whichever of these fits how you run the app:

- **`.env` file (recommended for local dev)** — copy `.env.example` to `.env` (which is gitignored) in the repo root and set your key. On startup the worker loads the nearest `.env` into the process environment, so it works both from `dotnet run` **and** the VS Code debugger without any shell setup:

  ```bash
  cp .env.example .env
  # then edit .env:  Solcast__ApiKey=<your-api-key>
  ```

- **Environment variable** — set it in your shell/service manager (double underscore separates config sections). A real environment variable always takes precedence over `.env`:

  ```bash
  export Solcast__ApiKey="<your-api-key>"
  ```

- **.NET user-secrets** — the `Solax.Worker` project has a `UserSecretsId`, so this works in Development too:

  ```bash
  cd src/Solax.Worker
  dotnet user-secrets set "Solcast:ApiKey" "<your-api-key>"
  ```

If the API key or resource id is missing, the worker logs a warning and skips forecast refreshes; the rest of the service continues to run. The free Solcast hobbyist tier caps daily API calls, which is why the forecast is cached and refreshed only every 12 hours by default — keep the interval within your plan's quota.

### Forecast-driven charge control (writes to the charger)

When enabled, the worker drives the EV charger from the solar forecast: while a car is connected it fast-charges on predicted surplus (`predictedSolar − OtherLoads`), pauses when the surplus falls below the minimum viable current, and restores the charger's original settings when the car is unplugged. It writes only values that differ from what's already on the device and logs every change.

```jsonc
"ChargeControl": {
  "Enabled": false,             // master switch — OFF by default (see warning)
  "DryRun": false,              // when Enabled: log intended writes but don't write (validation)
  "NominalVoltage": 230,
  "MinChargingCurrentAmps": 6,
  "MaxChargingCurrentAmps": 20, // setpoint is clamped to this
  "CurrentStepAmps": 1,         // whole-amp granularity the charger accepts
  "ResumeHysteresisWatts": 200  // extra surplus needed to (re)start, to avoid flapping
}
```

The current setpoint is encoded to the SolaX hardware's requirements automatically: rounded to a whole amp, clamped to the charger's **6–32 A** range, and written with the register's **0.01 A scale** (value = amps × 100). Pausing switches the use-mode to `Stop` and leaves the current setpoint untouched (0 A would be below the hardware minimum).

**Validate first with `DryRun`.** Set `Enabled: true` and `DryRun: true` to run the full control loop and log exactly what it *would* write — e.g. `[DRY RUN] would set charger current setpoint: 6A -> 16A (register 1600)` — without touching the charger. This is the safe way to confirm the register values against your device before allowing real writes.

> ⚠️ **This feature writes to your charger's Modbus holding registers.** The control-register addresses (`ChargerUseMode 0x60D`, `ChargeCurrentSetpoint 0x628`) and `EvChargerMode` values come from the SolaX X1/X3-HAC protocol / the wills106 register map, but **GEN1/GEN2 and firmware differences exist** — GEN1 uses Datahub Charge Current `0x624`, some GEN2 units use EVSE Mode `0x669`. **Verify them against your specific charger before setting `Enabled: true`.** It is disabled by default for exactly this reason.

## License

Licensed under the [MIT License](LICENSE).
