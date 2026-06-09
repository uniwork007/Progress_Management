using System;

namespace Progress_Management.Models;

public enum ScheduleWarningScope
{
    Project,
    Worker
}

public enum ScheduleWarningKind
{
    DependencyInversion,
    ActualOverrun,
    WorkerPlanConflict,
    WorkerActualDelay
}

public sealed class ScheduleWarning
{
    public ScheduleWarningScope Scope { get; init; }

    public ScheduleWarningKind Kind { get; init; }

    public string TaskId { get; init; } = "";

    public string? RelatedTaskId { get; init; }

    public string Message { get; init; } = "";

    public bool Involves(string taskId)
    {
        return string.Equals(TaskId, taskId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(RelatedTaskId, taskId, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesDependency(string predecessorTaskId, string successorTaskId)
    {
        return Kind == ScheduleWarningKind.DependencyInversion &&
            string.Equals(TaskId, successorTaskId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(RelatedTaskId, predecessorTaskId, StringComparison.OrdinalIgnoreCase);
    }
}
