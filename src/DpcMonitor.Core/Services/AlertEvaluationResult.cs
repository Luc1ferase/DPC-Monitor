using DpcMonitor.Core.Models;

namespace DpcMonitor.Core.Services;

public sealed record AlertEvaluationResult(MonitorState State, string? LogMessage);
