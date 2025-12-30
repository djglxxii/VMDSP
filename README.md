# Device Simulator POC

A .NET 8 LTS solution for simulating medical device protocols (HL7 MLLP, ASTM, POCT1-A).

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                       DeviceSimulator.Host                       │
│                    (ASP.NET Core Minimal API)                    │
├─────────────────────────────────────────────────────────────────┤
│  Orchestrator: start/stop devices, load packs, trigger scenarios │
└─────────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────┐    ┌──────────────────┐    ┌─────────────────┐
│  Transport  │    │  BehaviorRuntime │    │    Scenarios    │
│  Adapters   │    │  (State Machine) │    │  (Scheduler +   │
│  (TCP/MLLP) │    │  + Pack Loader   │    │   Fault Inject) │
└─────────────┘    └──────────────────┘    └─────────────────┘
         │                    │
         ▼                    ▼
┌─────────────────────────────────────────┐
│           Protocol Engines              │
│  HL7 MLLP  │  ASTM E1381  │  POCT1-A    │
└─────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│  Observability  │     Persistence       │
│  (Tracing)      │     (SQLite)          │
└─────────────────────────────────────────┘
```

## Projects

| Project | Description |
|---------|-------------|
| `DeviceSimulator.Abstractions` | Core interfaces and contracts |
| `DeviceSimulator.Transport` | TCP server/client primitives |
| `DeviceSimulator.Protocols.Hl7` | HL7 MLLP framing + ACK builder |
| `DeviceSimulator.Protocols.Astm` | ASTM control char framing + checksum |
| `DeviceSimulator.Protocols.Poct1A` | POCT1-A session/topic handling |
| `DeviceSimulator.BehaviorRuntime` | Behavior Pack loader + state machine |
| `DeviceSimulator.Scenarios` | Scenario scheduler + fault injection |
| `DeviceSimulator.Observability` | Tracing models + sinks |
| `DeviceSimulator.Persistence` | SQLite persistence |
| `DeviceSimulator.Host` | ASP.NET Core API orchestrator |
| `DeviceSimulator.Cli` | Command-line interface |

## Prerequisites

- .NET 8.0 SDK

## Build

```bash
dotnet build DeviceSimulator.sln
```

## Test

```bash
dotnet test DeviceSimulator.sln
```

## Run Host

```bash
dotnet run --project src/DeviceSimulator.Host
```

The API will be available at `http://localhost:5000` (or configured port).

### API Endpoints

- `GET /packs` - List loaded behavior packs
- `POST /packs/load?path={path}` - Load a behavior pack from path
- `POST /devices/start` - Start simulated devices `{ packName, count }`
- `POST /devices/stop` - Stop devices `{ groupId }`

## Run CLI

```bash
dotnet run --project src/DeviceSimulator.Cli -- --packPath ./behavior-packs/SampleAstmDevice --count 1
```

## Behavior Packs

Behavior packs are code-less plugins that define device behavior:

```
behavior-packs/
└── SampleAstmDevice/
    ├── manifest.yaml      # Pack metadata
    ├── protocol.yaml      # Protocol configuration
    ├── workflow.yaml      # State machine definition
    ├── quirks.yaml        # Device quirks/timing
    └── scenarios.yaml     # Test scenarios
```

## Next Steps (Phase 5+)

- [ ] Full HL7 message parsing (MSH, PID, OBR, OBX segments)
- [ ] Complete ASTM E1381 state machine with checksums
- [ ] POCT1-A topic subscription handling
- [ ] Real TCP transport implementation
- [ ] OpenTelemetry integration
- [ ] SQLite persistence implementation
- [ ] Fault injection (delays, disconnects, malformed data)
- [ ] Web UI dashboard

## License

Proprietary - Internal POC
