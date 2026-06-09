using Progress_Management.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Progress_Management.Services;

public static class ScheduleWarningService
{
    public static List<ScheduleWarning> Analyze(IEnumerable<WorkTask> tasks)
    {
        try
        {
            if (tasks == null)
            {
                Debug.WriteLine("Warning: ScheduleWarningService.Analyze received null tasks");
                return new List<ScheduleWarning>();
            }

            var taskList = tasks.ToList();
            var warnings = new List<ScheduleWarning>();
            var taskById = taskList.ToDictionary(task => task.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var task in taskList)
        {
            AddProjectWarnings(task, taskById, warnings);
        }

        foreach (var workerTasks in taskList.GroupBy(task => task.Worker))
            {
                AddWorkerWarnings(workerTasks.OrderBy(PlannedStart).ToList(), warnings);
            }

            return warnings;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in ScheduleWarningService.Analyze: {ex.Message}");
            return new List<ScheduleWarning>();
        }
    }

    private static void AddProjectWarnings(WorkTask task, IReadOnlyDictionary<string, WorkTask> taskById, List<ScheduleWarning> warnings)
    {
        foreach (var predecessorId in task.DependsOn)
        {
            if (!taskById.TryGetValue(predecessorId, out var predecessor))
            {
                warnings.Add(new ScheduleWarning
                {
                    Scope = ScheduleWarningScope.Project,
                    Kind = ScheduleWarningKind.DependencyInversion,
                    TaskId = task.Id,
                    RelatedTaskId = predecessorId,
                    Message = $"先行タスク {predecessorId} が見つかりません。"
                });
                continue;
            }

            var predecessorEnd = EffectiveEnd(predecessor);
            var successorStart = EffectiveStart(task);
            if (predecessorEnd.HasValue && successorStart.HasValue && predecessorEnd.Value > successorStart.Value)
            {
                warnings.Add(new ScheduleWarning
                {
                    Scope = ScheduleWarningScope.Project,
                    Kind = ScheduleWarningKind.DependencyInversion,
                    TaskId = task.Id,
                    RelatedTaskId = predecessor.Id,
                    Message = $"先行後続矛盾: {predecessor.Id} の終了日({Display(predecessorEnd.Value)})が {task.Id} の開始日({Display(successorStart.Value)})より後です。"
                });
            }
        }

        var plannedEnd = PlannedEnd(task);
        var actualEnd = RangeEnd(task.Actual);
        if (plannedEnd.HasValue && actualEnd.HasValue && actualEnd.Value > plannedEnd.Value)
        {
            warnings.Add(new ScheduleWarning
            {
                Scope = ScheduleWarningScope.Project,
                Kind = ScheduleWarningKind.ActualOverrun,
                TaskId = task.Id,
                Message = $"実績遅れ: {task.Id} の実績終了日({Display(actualEnd.Value)})が予定終了日({Display(plannedEnd.Value)})を超えています。"
            });
        }
    }

    private static void AddWorkerWarnings(IReadOnlyList<WorkTask> tasks, List<ScheduleWarning> warnings)
    {
        for (var i = 0; i < tasks.Count; i++)
        {
            var current = tasks[i];
            var currentStart = PlannedStart(current);
            var currentEnd = PlannedEnd(current);
            if (!currentStart.HasValue || !currentEnd.HasValue)
            {
                continue;
            }

            for (var j = i + 1; j < tasks.Count; j++)
            {
                var next = tasks[j];
                var nextStart = PlannedStart(next);
                var nextEnd = PlannedEnd(next);
                if (!nextStart.HasValue || !nextEnd.HasValue)
                {
                    continue;
                }

                if (currentStart.Value <= nextEnd.Value && nextStart.Value <= currentEnd.Value)
                {
                    warnings.Add(new ScheduleWarning
                    {
                        Scope = ScheduleWarningScope.Worker,
                        Kind = ScheduleWarningKind.WorkerPlanConflict,
                        TaskId = current.Id,
                        RelatedTaskId = next.Id,
                        Message = $"担当者予定矛盾: {current.Worker} の {current.Id} と {next.Id} の予定期間が重複しています。"
                    });
                }
            }

            if (i + 1 >= tasks.Count)
            {
                continue;
            }

            var actualEnd = RangeEnd(current.Actual);
            var nextPlannedStart = PlannedStart(tasks[i + 1]);
            if (actualEnd.HasValue && nextPlannedStart.HasValue && actualEnd.Value > nextPlannedStart.Value)
            {
                warnings.Add(new ScheduleWarning
                {
                    Scope = ScheduleWarningScope.Worker,
                    Kind = ScheduleWarningKind.WorkerActualDelay,
                    TaskId = current.Id,
                    RelatedTaskId = tasks[i + 1].Id,
                    Message = $"担当者実績遅れ: {current.Id} の実績終了日({Display(actualEnd.Value)})が次タスク {tasks[i + 1].Id} の開始日({Display(nextPlannedStart.Value)})を超えています。"
                });
            }
        }
    }

    private static DateTime? PlannedStart(WorkTask task) => RangeStart(task.Revised) ?? RangeStart(task.Baseline);

    private static DateTime? PlannedEnd(WorkTask task) => RangeEnd(task.Revised) ?? RangeEnd(task.Baseline);

    private static DateTime? EffectiveStart(WorkTask task) => RangeStart(task.Proposal) ?? PlannedStart(task);

    private static DateTime? EffectiveEnd(WorkTask task) => RangeEnd(task.Actual) ?? PlannedEnd(task);

    private static DateTime? RangeStart(string[]? range) => range is { Length: >= 2 } ? Parse(range[0]) : null;

    private static DateTime? RangeEnd(string[]? range) => range is { Length: >= 2 } ? Parse(range[1]) : null;

    private static DateTime? Parse(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            return DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch (FormatException ex)
        {
            Debug.WriteLine($"Warning: Invalid date format '{value}': {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error parsing date '{value}': {ex.Message}");
            return null;
        }
    }

    private static string Display(DateTime value) => value.ToString("yyyy/MM/dd", CultureInfo.GetCultureInfo("ja-JP"));
}
