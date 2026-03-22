# DPC Monitor Design

Date: 2026-03-22
Topic: Windows native DPC real-time monitoring GUI
Status: Approved for planning

## Goals

Build a Windows native desktop application that monitors local-machine DPC latency in real time and presents it in a GUI.

The first version must provide:
- Real-time monitoring of DPC/ISR execution latency on the local machine
- A real-time scrolling line chart view
- Current, peak, and average latency indicators
- Runtime-editable threshold configuration
- In-app alerting when the threshold is exceeded
- No custom kernel driver installation
- No persistent Windows service installation

## Constraints And Assumptions

- Platform: Windows only
- App style: native desktop application
- Preferred stack: C# with WPF
- Privilege model: admin elevation is acceptable
- Installation model: no custom driver, no always-on service
- Scope: local machine only
- V1 priority: real-time monitor plus threshold alerts

In this design, "DPC real-time latency" means ETW-observed DPC/ISR execution-time statistics, reported in microseconds. This is sufficient for a practical first release with live monitoring and alerting, but it is not a hardware-level latency probe implemented through a custom driver.

## Approaches Considered

### 1. Recommended: WPF + ETW + in-process aggregation

Use a WPF desktop app that starts a real-time kernel ETW session, consumes DPC/ISR events, aggregates them into one-second buckets, and updates the UI in process.

Pros:
- Native Windows UX
- Self-contained application
- No driver or service required
- Good fit for live charting and alert state management
- Clear ownership of sampling, aggregation, and UI refresh

Cons:
- ETW session management is non-trivial
- Requires admin privileges
- Must handle kernel session conflicts and teardown cleanly

### 2. Alternative: WPF + external Windows tooling

Wrap or invoke external performance tooling and surface the parsed results in a WPF UI.

Pros:
- Less custom ETW code
- Faster prototype path

Cons:
- Adds external runtime dependency
- Harder to package as a clean standalone app
- Weaker control over sampling cadence and alert behavior

### 3. Rejected: Approximate polling via generic performance counters

Sample indirect counters with a timer and infer DPC latency.

Pros:
- Simpler implementation
- No ETW session handling

Cons:
- Does not meet the requirement for credible real-time DPC latency monitoring
- Poor fidelity for threshold alerting
- Likely to mislead users

## Recommended Architecture

### Tech Stack

- Runtime: .NET 8
- UI: WPF
- UI pattern: MVVM
- ETW consumption: `Microsoft.Diagnostics.Tracing.TraceEvent` over a real-time kernel session
- Charting: WPF chart component optimized for rolling line updates
  - Preferred implementation direction: start with a lightweight WPF chart library or a custom `Polyline`-based chart if dependency weight becomes an issue during planning
- Logging: in-app session log pane for operator-visible events

### Major Components

#### 1. Elevation And Startup Layer

Responsibilities:
- Detect whether the process is elevated
- Block monitoring when the app is not running as administrator
- Offer an explicit restart-elevated flow
- Surface startup failures cleanly in the UI

#### 2. ETW Session Manager

Responsibilities:
- Start and stop a real-time kernel ETW session for DPC/ISR-related events
- Own session lifecycle and disposal
- Detect session-start conflicts and permission failures
- Expose parsed event records to the aggregation pipeline

Expected behavior:
- Monitoring begins only after the user clicks `Start`
- Monitoring stops immediately on `Stop`
- Session resources are always released on stop or app exit

#### 3. Event Aggregator

Responsibilities:
- Convert raw ETW events into one-second buckets
- Compute:
  - `current_us`: the maximum DPC/ISR execution time observed in the most recent completed one-second bucket
  - `peak_us`: the maximum DPC/ISR execution time observed since the current monitoring session started
  - `average_us`: the arithmetic mean of all observed DPC/ISR execution times since the current monitoring session started
  - threshold state for the bucket
- Maintain a fixed-size rolling window for chart rendering

Bucket policy:
- Cadence: 1 second
- Initial chart window: 60 seconds
- Empty bucket policy: if no valid events arrive in a second, write a zero-value or no-activity point without failing the session

#### 4. Alert Engine

Responsibilities:
- Compare the latest bucket against the configured threshold
- Emit state-change events when the app enters alert or recovers from alert
- Avoid log spam during sustained alert periods
- Emit additional log entries when a new peak is set

V1 alert policy:
- Default threshold: 300 us
- Editable at runtime
- UI-only alerting in V1
- No tray notifications, sound alerts, or Windows toast notifications in V1

#### 5. UI And ViewModels

Responsibilities:
- Bind top-level metric cards, chart points, current status, and log entries
- Keep chart updates responsive without freezing the UI thread
- Reflect monitoring state transitions: idle, running, alerting, error

## Main Window Design

### Layout

Top section:
- `Current`
- `Peak`
- `Average`
- `Threshold`

Center section:
- Real-time scrolling line chart
- One-second update cadence
- 60-second rolling window
- Each plotted point represents the `current_us` value of a completed one-second bucket
- Threshold reference line
- Current point highlighted during alert state

Right-side utility section:
- Threshold editor
- Start and Stop controls
- Monitoring state indicator
- Short alert summary panel

Bottom section:
- Session log with timestamped operator-visible events

### Visual Behavior

- Normal state uses neutral colors
- Alert state changes the current metric card, current plotted point, and status indicator to warning or error colors
- Recovery from alert returns the UI to normal colors and appends a recovery log event
- `Stop` preserves the last visible dataset until the next session start or manual reset

## Data Flow

1. User clicks `Start`
2. App validates elevation and attempts to create the ETW kernel session
3. ETW session manager receives DPC/ISR events in real time
4. Event aggregator folds raw events into one-second buckets
5. Alert engine evaluates the newest bucket against the threshold using `current_us`
6. ViewModels receive the new point and state updates
7. UI refreshes metric cards, chart, status, and log pane
8. User clicks `Stop`, or the session faults and transitions to `Error`

## State Model

### Idle
- App launched, no active ETW session
- Chart may show the last stopped session until cleared

### Running
- ETW session active
- Aggregator and chart updating once per second

### Alerting
- A running substate entered when the latest bucket exceeds the threshold
- Metrics and chart styling switch to alert visuals

### Error
- ETW startup failure, permission denial, parser failure, or unexpected background exception
- Monitoring halts
- UI stays responsive and displays the failure in the log and state banner

## Error Handling

### Permission Failures
- If the app is not elevated, monitoring does not start
- UI shows a clear message that administrator rights are required
- App may offer a restart-elevated action

### ETW Session Conflicts
- If the kernel session cannot be started because of conflict or access denial, monitoring transitions to `Error`
- Failure reason is written to the session log and surfaced in status UI
- User may retry after resolving the conflict

### Bad Or Unexpected Event Data
- Invalid or malformed individual events are dropped
- The session remains alive unless the failure indicates a parser-wide fault
- A limited diagnostic log entry may be written for visibility

### Background Pipeline Failures
- If the ETW reader or aggregator throws unexpectedly, the app stops monitoring gracefully
- Session resources are disposed
- UI stays alive and reports the failure

### Shutdown And Stop
- On `Stop`, application shutdown, or fatal background error, the ETW session is always closed
- Background workers are canceled cooperatively
- The design must avoid orphaned ETW sessions

## Testing Strategy

### Unit Tests

Cover pure logic for:
- One-second bucket aggregation
- Rolling-window trimming
- Peak tracking
- Average calculation
- Alert-state transitions
- Alert recovery
- Log deduplication during sustained threshold breaches

### Integration Tests

Cover app-level coordination for:
- Start and stop command flow
- ViewModel state transitions
- Chart-window update behavior
- Error-state rendering behavior

The ETW reader should be abstracted behind an interface so integration tests can inject a fake event source.

### Manual Validation

Manual checks for V1:
- Launch without admin rights and verify the app blocks monitoring with a clear message
- Launch with admin rights and verify ETW session start succeeds
- Confirm the chart scrolls once per second and each new point matches the latest completed bucket
- Confirm threshold exceedance based on `current_us` changes UI state and writes a log entry
- Confirm alert recovery writes a recovery log entry
- Confirm stop releases capture resources and does not leave a broken next-start state

## Out Of Scope For V1

- CSV export
- Historical persistence across launches
- Multi-machine monitoring
- Driver-based or service-based low-level latency probing
- Tray integration
- Windows toast notifications
- Audio alerts
- Per-driver or per-core drill-down views

## Risks And Mitigations

### Risk: ETW kernel session startup complexity
Mitigation:
- Isolate ETW lifecycle management behind a dedicated service
- Validate failure paths early during implementation

### Risk: Session conflicts or permissions vary by machine
Mitigation:
- Build explicit error-state UX and retry flow
- Keep logging operator-readable

### Risk: Real-time chart updates can overwhelm the UI if implemented poorly
Mitigation:
- Use fixed-size rolling data structures
- Update on a controlled cadence of one second
- Marshal only final bucket results to the UI thread

## Acceptance Criteria

The design is considered satisfied when the first release can:
- Run as a Windows native desktop app
- Require admin rights but not require a custom driver or Windows service
- Start and stop a real-time monitoring session on demand
- Show current, peak, and average latency values in microseconds
- Show a real-time scrolling line chart over a rolling window
- Let the user edit a threshold at runtime
- Enter and recover from an in-app alert state based on threshold crossings
- Keep a readable session log of state changes and notable events

## Repository Notes

The current workspace does not contain an existing Git repository. The design document can be written into the workspace, but the "commit the design doc" step is blocked until the project is initialized as a Git repository or moved into one.


