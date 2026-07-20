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

## Getting started

> Implementation has not started yet. This section will be filled in with build, configuration, and run instructions once the initial service scaffold lands.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Network access to the SolaX inverter and EV charger with Modbus TCP enabled

## Configuration

Configuration (device IP addresses, Modbus ports/unit IDs, polling intervals, charging strategy parameters) will be documented here once implemented.

## License

Licensed under the [MIT License](LICENSE).
