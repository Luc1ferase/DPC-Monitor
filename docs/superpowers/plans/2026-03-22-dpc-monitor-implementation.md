# DPC Monitor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows-native WPF application that monitors local DPC/ISR execution latency in real time, renders a rolling line chart, and raises in-app threshold alerts.

**Architecture:** Create a small solution with a pure-domain core, a thin ETW adapter, and a WPF app shell. Keep ETW session lifecycle isolated behind interfaces, drive the core with TDD, and let the UI bind to completed one-second buckets rather than raw events.

**Tech Stack:** .NET 8 target framework, C#, WPF, xUnit, `Microsoft.Diagnostics.Tracing.TraceEvent`, MVVM with hand-rolled commands/view-model base classes

---

## Repository And File Map

### Create

- `.gitignore`
- `README.md`
- `DpcMonitor.sln`
- `Directory.Build.props`
- `src/DpcMonitor.Core/DpcMonitor.Core.csproj`
- `src/DpcMonitor.Core/Models/DpcLatencyBucket.cs`
- `src/DpcMonitor.Core/Models/DpcLatencySample.cs`
- `src/DpcMonitor.Core/Models/MonitorState.cs`
- `src/DpcMonitor.Core/Models/MonitorStatusSnapshot.cs`
- `src/DpcMonitor.Core/Services/IDpcEventSource.cs`
- `src/DpcMonitor.Core/Services/IMonitorCoordinator.cs`
- `src/DpcMonitor.Core/Services/IPrivilegeService.cs`
- `src/DpcMonitor.Core/Services/ILatencyClock.cs`
- `src/DpcMonitor.Core/Services/DpcLatencyAggregator.cs`
- `src/DpcMonitor.Core/Services/AlertEvaluator.cs`
- `src/DpcMonitor.Core/Services/SessionLogBuffer.cs`
- `src/DpcMonitor.Core/Services/MonitorCoordinator.cs`
- `src/DpcMonitor.Etw/DpcMonitor.Etw.csproj`
- `src/DpcMonitor.Etw/EtwDpcEventSource.cs`
- `src/DpcMonitor.Etw/KernelTraceSessionFactory.cs`
- `src/DpcMonitor.Etw/WindowsPrivilegeService.cs`
- `src/DpcMonitor.App/DpcMonitor.App.csproj`
- `src/DpcMonitor.App/App.xaml`
- `src/DpcMonitor.App/App.xaml.cs`
- `src/DpcMonitor.App/MainWindow.xaml`
- `src/DpcMonitor.App/MainWindow.xaml.cs`
- `src/DpcMonitor.App/ViewModels/ViewModelBase.cs`
- `src/DpcMonitor.App/ViewModels/MainWindowViewModel.cs`
- `src/DpcMonitor.App/Commands/RelayCommand.cs`
- `src/DpcMonitor.App/Converters/SamplesToPointCollectionConverter.cs`
- `src/DpcMonitor.App/Resources/StatusBrushes.xaml`
- `tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj`
- `tests/DpcMonitor.Core.Tests/DpcLatencyAggregatorTests.cs`
- `tests/DpcMonitor.Core.Tests/AlertEvaluatorTests.cs`
- `tests/DpcMonitor.Core.Tests/SessionLogBufferTests.cs`
- `tests/DpcMonitor.Core.Tests/MonitorCoordinatorTests.cs`
- `tests/DpcMonitor.Etw.Tests/DpcMonitor.Etw.Tests.csproj`
- `tests/DpcMonitor.Etw.Tests/EtwContractTests.cs`
- `tests/DpcMonitor.App.Tests/DpcMonitor.App.Tests.csproj`
- `tests/DpcMonitor.App.Tests/MainWindowViewModelTests.cs`

### Modify

- [2026-03-22-dpc-monitor-design.md](/X:/Repos/oss/dpc-monitor/docs/superpowers/specs/2026-03-22-dpc-monitor-design.md) only if implementation reveals a spec mismatch

### Notes

- The repository currently has no commits yet. Do not attempt worktree-based execution until there is at least one baseline commit.
- Ignore `.superpowers/`, `.worktrees/`, `worktrees/`, `bin/`, `obj/`, and `.vs/` before any commit.
- Keep the chart implementation dependency-light. Use a WPF `Polyline`-based renderer first; do not add a charting library unless the custom approach proves insufficient.

## Task 1: Bootstrap The Repository And Solution

**Files:**
- Create: `.gitignore`
- Create: `README.md`
- Create: `Directory.Build.props`
- Create: `DpcMonitor.sln`
- Create: `src/DpcMonitor.Core/DpcMonitor.Core.csproj`
- Create: `src/DpcMonitor.Etw/DpcMonitor.Etw.csproj`
- Create: `src/DpcMonitor.App/DpcMonitor.App.csproj`
- Create: `tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj`
- Create: `tests/DpcMonitor.Etw.Tests/DpcMonitor.Etw.Tests.csproj`
- Create: `tests/DpcMonitor.App.Tests/DpcMonitor.App.Tests.csproj`

- [ ] **Step 1: Add ignore rules and repository notes**

```gitignore
.superpowers/
.worktrees/
worktrees/
.vs/
**/bin/
**/obj/
TestResults/
```

- [ ] **Step 2: Create the solution and project skeleton**

Run:

```powershell
dotnet new sln -n DpcMonitor
dotnet new classlib -n DpcMonitor.Core -o src/DpcMonitor.Core -f net8.0
dotnet new classlib -n DpcMonitor.Etw -o src/DpcMonitor.Etw -f net8.0
dotnet new wpf -n DpcMonitor.App -o src/DpcMonitor.App -f net8.0
dotnet new xunit -n DpcMonitor.Core.Tests -o tests/DpcMonitor.Core.Tests -f net8.0
dotnet new xunit -n DpcMonitor.Etw.Tests -o tests/DpcMonitor.Etw.Tests -f net8.0
dotnet new xunit -n DpcMonitor.App.Tests -o tests/DpcMonitor.App.Tests -f net8.0
dotnet sln DpcMonitor.sln add src/DpcMonitor.Core/DpcMonitor.Core.csproj src/DpcMonitor.Etw/DpcMonitor.Etw.csproj src/DpcMonitor.App/DpcMonitor.App.csproj tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj tests/DpcMonitor.Etw.Tests/DpcMonitor.Etw.Tests.csproj tests/DpcMonitor.App.Tests/DpcMonitor.App.Tests.csproj
dotnet add src/DpcMonitor.Etw/DpcMonitor.Etw.csproj reference src/DpcMonitor.Core/DpcMonitor.Core.csproj
dotnet add src/DpcMonitor.App/DpcMonitor.App.csproj reference src/DpcMonitor.Core/DpcMonitor.Core.csproj src/DpcMonitor.Etw/DpcMonitor.Etw.csproj
dotnet add tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj reference src/DpcMonitor.Core/DpcMonitor.Core.csproj
dotnet add tests/DpcMonitor.Etw.Tests/DpcMonitor.Etw.Tests.csproj reference src/DpcMonitor.Core/DpcMonitor.Core.csproj src/DpcMonitor.Etw/DpcMonitor.Etw.csproj
dotnet add tests/DpcMonitor.App.Tests/DpcMonitor.App.Tests.csproj reference src/DpcMonitor.Core/DpcMonitor.Core.csproj src/DpcMonitor.App/DpcMonitor.App.csproj
```

- [ ] **Step 3: Add shared build properties**

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Add a minimal README with prerequisites**

```markdown
# DPC Monitor

Windows-native WPF tool for observing ETW-backed DPC/ISR latency buckets.

## Prerequisites
- Windows
- .NET SDK 8 or newer
- Administrator rights to start live monitoring
```

- [ ] **Step 5: Build the empty scaffold**

Run: `dotnet build DpcMonitor.sln`

Expected: PASS with all projects restoring and compiling.

- [ ] **Step 6: Commit the scaffold**

```powershell
git add .gitignore README.md Directory.Build.props DpcMonitor.sln src tests
git commit -m "chore: scaffold dpc monitor solution"
```

## Task 2: Build The Aggregation Domain With TDD

**Files:**
- Create: `src/DpcMonitor.Core/Models/DpcLatencySample.cs`
- Create: `src/DpcMonitor.Core/Models/DpcLatencyBucket.cs`
- Create: `src/DpcMonitor.Core/Services/DpcLatencyAggregator.cs`
- Test: `tests/DpcMonitor.Core.Tests/DpcLatencyAggregatorTests.cs`

- [ ] **Step 1: Write the failing aggregation tests**

```csharp
[Fact]
public void CompleteBucket_UsesMaximumLatencyAsCurrentValue()
{
    var aggregator = new DpcLatencyAggregator(windowSize: 3);
    aggregator.Add(new DpcLatencySample(new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero), 120));
    aggregator.Add(new DpcLatencySample(new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero).AddMilliseconds(400), 260));
    aggregator.Add(new DpcLatencySample(new DateTimeOffset(2026, 3, 22, 10, 0, 1, TimeSpan.Zero), 80));

    var completed = aggregator.DrainCompletedBuckets().Single();

    Assert.Equal(260, completed.CurrentUs);
    Assert.Equal(260, completed.PeakUs);
    Assert.Equal(190.0, completed.AverageUs);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj --filter CompleteBucket_UsesMaximumLatencyAsCurrentValue`

Expected: FAIL because the aggregator and model types do not exist yet.

- [ ] **Step 3: Implement the minimal aggregation types**

```csharp
public sealed record DpcLatencySample(DateTimeOffset Timestamp, double DurationUs);

public sealed record DpcLatencyBucket(
    DateTimeOffset BucketStart,
    double CurrentUs,
    double PeakUs,
    double AverageUs);
```

Implement `DpcLatencyAggregator` so that:
- a bucket is keyed by whole-second UTC timestamp
- `CurrentUs` is the max duration within that completed second
- `PeakUs` is session-wide max so far
- `AverageUs` is the arithmetic mean across all observed samples so far
- only completed buckets are emitted

- [ ] **Step 4: Add rolling-window coverage**

Write a second failing test that emits four completed buckets into a `windowSize: 3` aggregator and expects only the latest three buckets in the retained chart history.

- [ ] **Step 5: Run the core test suite**

Run: `dotnet test tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj`

Expected: PASS with the aggregation tests green.

- [ ] **Step 6: Commit the domain aggregation slice**

```powershell
git add src/DpcMonitor.Core/Models/DpcLatencySample.cs src/DpcMonitor.Core/Models/DpcLatencyBucket.cs src/DpcMonitor.Core/Services/DpcLatencyAggregator.cs tests/DpcMonitor.Core.Tests/DpcLatencyAggregatorTests.cs
git commit -m "feat: add latency aggregation core"
```

## Task 3: Add Alert Evaluation And Session Log Deduplication

**Files:**
- Create: `src/DpcMonitor.Core/Models/MonitorState.cs`
- Create: `src/DpcMonitor.Core/Services/AlertEvaluator.cs`
- Create: `src/DpcMonitor.Core/Services/SessionLogBuffer.cs`
- Test: `tests/DpcMonitor.Core.Tests/AlertEvaluatorTests.cs`
- Test: `tests/DpcMonitor.Core.Tests/SessionLogBufferTests.cs`

- [ ] **Step 1: Write the failing alert-state tests**

```csharp
[Fact]
public void Evaluate_TransitionsToAlerting_WhenCurrentUsExceedsThreshold()
{
    var evaluator = new AlertEvaluator();
    var bucket = new DpcLatencyBucket(
        new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero),
        CurrentUs: 352,
        PeakUs: 352,
        AverageUs: 240);

    var result = evaluator.Evaluate(bucket, thresholdUs: 300, previousState: MonitorState.Running);

    Assert.Equal(MonitorState.Alerting, result.State);
    Assert.Contains("352", result.LogMessage);
}
```

- [ ] **Step 2: Run the alert test to verify it fails**

Run: `dotnet test tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj --filter Evaluate_TransitionsToAlerting_WhenCurrentUsExceedsThreshold`

Expected: FAIL because the alert evaluator does not exist.

- [ ] **Step 3: Implement `MonitorState`, `AlertEvaluator`, and `SessionLogBuffer`**

```csharp
public enum MonitorState
{
    Idle,
    Running,
    Alerting,
    Error
}
```

Implementation requirements:
- enter alert state when `CurrentUs > thresholdUs`
- recover to running when `CurrentUs <= thresholdUs`
- emit log messages only on state transition or new peak
- suppress repeated identical alert lines during sustained breach periods

- [ ] **Step 4: Add a failing log-buffer dedupe test**

Write a test that appends the same alert line twice and asserts that only one visible entry is retained unless the text changes.

- [ ] **Step 5: Run the core test suite**

Run: `dotnet test tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj`

Expected: PASS with alert and log behavior covered.

- [ ] **Step 6: Commit the alert slice**

```powershell
git add src/DpcMonitor.Core/Models/MonitorState.cs src/DpcMonitor.Core/Services/AlertEvaluator.cs src/DpcMonitor.Core/Services/SessionLogBuffer.cs tests/DpcMonitor.Core.Tests/AlertEvaluatorTests.cs tests/DpcMonitor.Core.Tests/SessionLogBufferTests.cs
git commit -m "feat: add alert evaluation and session logging"
```

## Task 4: Coordinate Start, Stop, And Bucket Publication

**Files:**
- Create: `src/DpcMonitor.Core/Models/MonitorStatusSnapshot.cs`
- Create: `src/DpcMonitor.Core/Services/IDpcEventSource.cs`
- Create: `src/DpcMonitor.Core/Services/IMonitorCoordinator.cs`
- Create: `src/DpcMonitor.Core/Services/IPrivilegeService.cs`
- Create: `src/DpcMonitor.Core/Services/ILatencyClock.cs`
- Create: `src/DpcMonitor.Core/Services/MonitorCoordinator.cs`
- Test: `tests/DpcMonitor.Core.Tests/MonitorCoordinatorTests.cs`

- [ ] **Step 1: Write the failing coordinator tests**

```csharp
[Fact]
public async Task StartAsync_PublishesErrorSnapshot_WhenPrivilegesAreInsufficient()
{
    var source = new FakeDpcEventSource();
    var privileges = new StubPrivilegeService(isElevated: false);
    var coordinator = new MonitorCoordinator(source, privileges, new ManualLatencyClock());

    var snapshot = await coordinator.StartAsync(thresholdUs: 300);

    Assert.Equal(MonitorState.Error, snapshot.State);
    Assert.Contains("administrator", snapshot.StatusMessage, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the coordinator test to verify it fails**

Run: `dotnet test tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj --filter StartAsync_PublishesErrorSnapshot_WhenPrivilegesAreInsufficient`

Expected: FAIL because the coordinator and interfaces do not exist.

- [ ] **Step 3: Implement the coordinator and interfaces**

Implementation requirements:
- expose an `IMonitorCoordinator` interface so the WPF layer can be tested against a fake coordinator
- `StartAsync` checks elevation first
- if not elevated, return an `Error` snapshot and do not start the event source
- when elevated, subscribe to the event source, feed samples to the aggregator, and publish completed-bucket snapshots
- `StopAsync` cancels subscriptions, flushes the session, and returns to `Idle`

- [ ] **Step 4: Add a second failing test for stop behavior**

Write a test that starts the coordinator with a fake event source, emits samples, calls `StopAsync`, and asserts that:
- the event source was stopped once
- the final state becomes `Idle`
- the last bucket history remains available to the caller

- [ ] **Step 5: Run the core test suite**

Run: `dotnet test tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj`

Expected: PASS with coordinator lifecycle covered.

- [ ] **Step 6: Commit the monitor orchestration slice**

```powershell
git add src/DpcMonitor.Core/Models/MonitorStatusSnapshot.cs src/DpcMonitor.Core/Services/IDpcEventSource.cs src/DpcMonitor.Core/Services/IMonitorCoordinator.cs src/DpcMonitor.Core/Services/IPrivilegeService.cs src/DpcMonitor.Core/Services/ILatencyClock.cs src/DpcMonitor.Core/Services/MonitorCoordinator.cs tests/DpcMonitor.Core.Tests/MonitorCoordinatorTests.cs
git commit -m "feat: add monitoring coordinator"
```

## Task 5: Implement The ETW Adapter Behind The Core Interfaces

**Files:**
- Create: `src/DpcMonitor.Etw/EtwDpcEventSource.cs`
- Create: `src/DpcMonitor.Etw/KernelTraceSessionFactory.cs`
- Create: `src/DpcMonitor.Etw/WindowsPrivilegeService.cs`
- Create: `tests/DpcMonitor.Etw.Tests/DpcMonitor.Etw.Tests.csproj`
- Create: `tests/DpcMonitor.Etw.Tests/EtwContractTests.cs`
- Modify: `src/DpcMonitor.Etw/DpcMonitor.Etw.csproj`

- [ ] **Step 1: Add the ETW package dependency**

Run:

```powershell
dotnet add src/DpcMonitor.Etw/DpcMonitor.Etw.csproj package Microsoft.Diagnostics.Tracing.TraceEvent
```

- [ ] **Step 2: Write a failing compile-level contract test**

Create a dedicated ETW test project and add a stub-based compile assertion that instantiates `WindowsPrivilegeService` and `EtwDpcEventSource` through the core interfaces:

```csharp
[Fact]
public void EtwImplementations_SatisfyCoreContracts()
{
    IPrivilegeService privileges = new WindowsPrivilegeService();
    Assert.NotNull(privileges);
}
```

Expected initial result: FAIL until the concrete types exist.

- [ ] **Step 3: Implement `WindowsPrivilegeService`**

Use `WindowsIdentity` and `WindowsPrincipal` to determine whether the current process belongs to the Administrator role.

- [ ] **Step 4: Implement `EtwDpcEventSource` and session factory**

Implementation requirements:
- own the `TraceEventSession` lifecycle in a dedicated class
- subscribe only to the kernel flags required for DPC/ISR monitoring
- translate each relevant ETW event into `DpcLatencySample`
- never push raw ETW types across the core boundary
- ensure `DisposeAsync`/stop tears down the kernel session exactly once

- [ ] **Step 5: Build and run the core tests**

Run:

```powershell
dotnet build DpcMonitor.sln
dotnet test tests/DpcMonitor.Core.Tests/DpcMonitor.Core.Tests.csproj
dotnet test tests/DpcMonitor.Etw.Tests/DpcMonitor.Etw.Tests.csproj
```

Expected: PASS; ETW adapter compiles cleanly even if live ETW is only covered manually.

- [ ] **Step 6: Commit the ETW integration slice**

```powershell
git add src/DpcMonitor.Etw tests/DpcMonitor.Etw.Tests
git commit -m "feat: add etw event source adapter"
```

## Task 6: Bind The WPF ViewModel And Rolling Chart

**Files:**
- Create: `src/DpcMonitor.App/ViewModels/ViewModelBase.cs`
- Create: `src/DpcMonitor.App/ViewModels/MainWindowViewModel.cs`
- Create: `src/DpcMonitor.App/Commands/RelayCommand.cs`
- Create: `src/DpcMonitor.App/Converters/SamplesToPointCollectionConverter.cs`
- Create: `src/DpcMonitor.App/Resources/StatusBrushes.xaml`
- Modify: `src/DpcMonitor.App/App.xaml`
- Modify: `src/DpcMonitor.App/MainWindow.xaml`
- Modify: `src/DpcMonitor.App/MainWindow.xaml.cs`
- Test: `tests/DpcMonitor.App.Tests/MainWindowViewModelTests.cs`

- [ ] **Step 1: Write the failing ViewModel tests**

```csharp
[Fact]
public async Task OnBucketCompleted_UpdatesCurrentPeakAverageAndChartSeries()
{
    var coordinator = new FakeMonitorCoordinator();
    var viewModel = new MainWindowViewModel(coordinator);

    await viewModel.StartAsync();
    coordinator.Publish(new MonitorStatusSnapshot(
        MonitorState.Running,
        CurrentUs: 184,
        PeakUs: 512,
        AverageUs: 129,
        ThresholdUs: 300,
        StatusMessage: "Running",
        Chart: new[] { new DpcLatencyBucket(DateTimeOffset.UtcNow, 184, 512, 129) }));

    Assert.Equal("184 us", viewModel.CurrentText);
    Assert.Single(viewModel.ChartBuckets);
}
```

- [ ] **Step 2: Run the ViewModel test to verify it fails**

Run: `dotnet test tests/DpcMonitor.App.Tests/DpcMonitor.App.Tests.csproj --filter OnBucketCompleted_UpdatesCurrentPeakAverageAndChartSeries`

Expected: FAIL because the ViewModel and fake coordinator bridge do not exist yet.

- [ ] **Step 3: Implement the ViewModel and command plumbing**

Implementation requirements:
- expose `CurrentText`, `PeakText`, `AverageText`, `ThresholdUs`, `IsRunning`, and `IsAlerting`
- surface `StartCommand`, `StopCommand`, and `RestartElevatedCommand`
- expose a read-only chart bucket collection capped at 60 items
- map coordinator snapshots directly into view state

- [ ] **Step 4: Implement the XAML layout**

Use:
- four metric cards at the top
- a `Canvas`/`Polyline` center chart with a threshold line
- a right-side control panel for threshold editing and run controls
- a bottom log list

The XAML should bind colors through `StatusBrushes.xaml` so alert visuals are data-driven.

- [ ] **Step 5: Run app tests and build**

Run:

```powershell
dotnet test tests/DpcMonitor.App.Tests/DpcMonitor.App.Tests.csproj
dotnet build DpcMonitor.sln
```

Expected: PASS with the UI project compiling cleanly.

- [ ] **Step 6: Commit the WPF UI slice**

```powershell
git add src/DpcMonitor.App tests/DpcMonitor.App.Tests
git commit -m "feat: add realtime monitoring ui"
```

## Task 7: Wire The Real App Composition And Manual Validation Flow

**Files:**
- Modify: `src/DpcMonitor.App/App.xaml.cs`
- Modify: `src/DpcMonitor.App/MainWindow.xaml.cs`
- Modify: `README.md`

- [ ] **Step 1: Write a failing composition smoke test if practical**

If app-level composition is testable in the current structure, add a smoke test that constructs `MainWindowViewModel` with the real `WindowsPrivilegeService` and a fake event source factory. If not, document the limitation and proceed with build + manual validation only.

- [ ] **Step 2: Compose the real services in the app startup path**

Implementation requirements:
- instantiate `WindowsPrivilegeService`
- instantiate `EtwDpcEventSource`
- instantiate `MonitorCoordinator`
- inject them into `MainWindowViewModel`
- keep `MainWindow.xaml.cs` as thin as possible

- [ ] **Step 3: Add restart-elevated support**

Use a `ProcessStartInfo` with `Verb = "runas"` to relaunch the executable on demand. Do not require elevation at app launch through the manifest.

- [ ] **Step 4: Document the manual validation procedure**

Add `README.md` sections for:
- running without admin rights
- restarting elevated
- expected alert behavior
- known ETW limitations and local-only scope

- [ ] **Step 5: Run the final verification commands**

Run:

```powershell
dotnet test DpcMonitor.sln
dotnet build DpcMonitor.sln
dotnet run --project src/DpcMonitor.App/DpcMonitor.App.csproj
```

Expected:
- tests PASS
- build PASS
- app launches
- when run without elevation, monitoring is blocked with a clear message
- when relaunched elevated, monitoring can be started manually

- [ ] **Step 6: Commit the composed application**

```powershell
git add README.md src tests
git commit -m "feat: deliver dpc monitor desktop app"
```

## Task 8: Finish The Branch And Publish

**Files:**
- Modify: none unless follow-up fixes are required

- [ ] **Step 1: Review the final diff**

Run: `git -c safe.directory=X:/Repos/oss/dpc-monitor diff --stat`

Expected: only intended solution, docs, and ignore-rule changes.

- [ ] **Step 2: Verify branch status**

Run: `git -c safe.directory=X:/Repos/oss/dpc-monitor status --short --branch`

Expected: clean working tree on the active implementation branch.

- [ ] **Step 3: Push the branch**

Run:

```powershell
git push -u origin $(git branch --show-current)
```

Expected: remote branch updated successfully.

- [ ] **Step 4: Capture post-push summary**

Record:
- commit hash
- pushed branch name
- any manual validation gaps that remain



