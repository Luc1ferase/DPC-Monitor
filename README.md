# DPC Monitor

Windows-native WPF tool for observing ETW-backed DPC/ISR latency buckets in real time.

## Prerequisites

- Windows
- .NET SDK 8 or newer
- Administrator rights if you want to start live monitoring

## Build

```powershell
dotnet restore DpcMonitor.sln
dotnet build DpcMonitor.sln -m:1
```

## Run

```powershell
dotnet run --project src/DpcMonitor.App/DpcMonitor.App.csproj -m:1
```

When the app opens:
- You can view the dashboard without administrator rights
- Live monitoring starts only after you click `Start Monitoring`
- If the current process is not elevated, the app switches to an error state and prompts you to use `Restart Elevated`

## What V1 Includes

- Real-time DPC / ISR execution-time monitoring via ETW
- Rolling line chart over the latest completed buckets
- Current, peak, and average latency cards
- Editable alert threshold
- In-app alert state and session log
- No custom driver installation
- No persistent Windows service

## Known Limitations

- The chart uses 1-second completed buckets instead of per-event plotting
- Alerts are in-app only; there are no toast, tray, or sound notifications yet
- The current build is most reliable with `dotnet build -m:1` on this repository layout
- Monitoring scope is local-machine only
