using Microsoft.Data.Sqlite;
using Progress_Management.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Progress_Management.Services;

public static class ScenarioRepository
{
    private const string DatabaseFileName = "progress_management.db";

    // MSIX実行時は LocalApplicationData がパッケージ配下の LocalCache にリダイレクトされる。
    // DBeaver等で確認する場合は、実行形態に応じた実体パスを見る必要がある。
    public static string 
        DatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Progress_Management",
            DatabaseFileName);

    public static ProgressScenarioSet Load()
    {
        // 画面側はJSON固定データ時代と同じモデルを使い、保存層だけSQLiteに差し替える。
        SQLitePCL.Batteries_V2.Init();
        EnsureDatabase();

        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        var result = new ProgressScenarioSet();
        using var scenarioCommand = connection.CreateCommand();
        scenarioCommand.CommandText = """
            SELECT id, name, purpose, due_status, proposal_status
            FROM scenarios
            ORDER BY sort_order, id;
            """;

        using var scenarioReader = scenarioCommand.ExecuteReader();
        while (scenarioReader.Read())
        {
            var scenario = new ProgressScenario
            {
                Id = scenarioReader.GetString(0),
                Name = scenarioReader.GetString(1),
                Purpose = scenarioReader.GetString(2),
                DueStatus = scenarioReader.GetString(3),
                ProposalStatus = scenarioReader.GetString(4)
            };
            result.Scenarios.Add(scenario);
        }

        foreach (var scenario in result.Scenarios)
        {
            scenario.Tasks.AddRange(LoadTasks(connection, scenario.Id));
        }

        return result;
    }

    public static List<LookupItem> LoadScenarioOptions()
    {
        EnsureDatabase();

        var items = new List<LookupItem>();
        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name
            FROM scenarios
            ORDER BY sort_order, id;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new LookupItem
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1)
            });
        }

        return items;
    }

    public static List<LookupItem> LoadWorkerOptions()
    {
        EnsureDatabase();

        var items = new List<LookupItem>();
        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name
            FROM workers
            ORDER BY sort_order, id;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new LookupItem
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1)
            });
        }

        return items;
    }

    public static List<WorkerEditRecord> LoadWorkersForEdit()
    {
        EnsureDatabase();

        var workers = new List<WorkerEditRecord>();
        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, sort_order
            FROM workers
            ORDER BY sort_order, id;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            workers.Add(new WorkerEditRecord
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2)
            });
        }

        return workers;
    }

    public static void SaveWorker(WorkerEditRecord record)
    {
        EnsureDatabase();

        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        if (record.SortOrder <= 0)
        {
            record.SortOrder = NextWorkerSortOrder(connection);
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO workers (id, name, sort_order)
            VALUES ($id, $name, $sort_order)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                sort_order = excluded.sort_order;
            """;
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$name", record.Name);
        command.Parameters.AddWithValue("$sort_order", record.SortOrder);
        command.ExecuteNonQuery();
    }

    public static bool DeleteWorker(string workerId, out string message)
    {
        EnsureDatabase();

        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = """
                SELECT COUNT(*)
                FROM work_tasks
                WHERE worker_id = $worker_id;
                """;
            countCommand.Parameters.AddWithValue("$worker_id", workerId);

            var usedTaskCount = Convert.ToInt32(countCommand.ExecuteScalar());
            if (usedTaskCount > 0)
            {
                message = $"この担当者は {usedTaskCount} 件の作業で使用中のため削除できません。";
                return false;
            }
        }

        using var deleteCommand = connection.CreateCommand();
        deleteCommand.CommandText = """
            DELETE FROM workers
            WHERE id = $worker_id;
            """;
        deleteCommand.Parameters.AddWithValue("$worker_id", workerId);
        deleteCommand.ExecuteNonQuery();

        message = "削除しました。";
        return true;
    }

    public static List<string> LoadTaskIds(string scenarioId)
    {
        EnsureDatabase();

        var taskIds = new List<string>();
        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id
            FROM work_tasks
            WHERE scenario_id = $scenario_id
            ORDER BY sort_order, id;
            """;
        command.Parameters.AddWithValue("$scenario_id", scenarioId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            taskIds.Add(reader.GetString(0));
        }

        return taskIds;
    }

    public static List<string> LoadAllTaskIds()
    {
        EnsureDatabase();

        var taskIds = new List<string>();
        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id
            FROM work_tasks
            ORDER BY id;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            taskIds.Add(reader.GetString(0));
        }

        return taskIds;
    }

    public static WorkTaskEditRecord? LoadWorkTaskForEdit(string taskId)
    {
        EnsureDatabase();

        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                id,
                scenario_id,
                name,
                worker_id,
                difficulty,
                planned_workload,
                actual_workload,
                output_unit,
                baseline_start,
                baseline_end,
                revised_start,
                revised_end,
                actual_start,
                actual_end,
                proposal_start,
                proposal_end,
                status,
                note,
                sort_order
            FROM work_tasks
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", taskId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var record = new WorkTaskEditRecord
        {
            Id = reader.GetString(0),
            ScenarioId = reader.GetString(1),
            Name = reader.GetString(2),
            WorkerId = reader.GetString(3),
            Difficulty = reader.GetInt32(4),
            PlannedWorkload = Convert.ToDecimal(reader.GetDouble(5)),
            ActualWorkload = Convert.ToDecimal(reader.GetDouble(6)),
            OutputUnit = reader.GetString(7),
            BaselineStart = reader.GetString(8),
            BaselineEnd = reader.GetString(9),
            RevisedStart = reader.IsDBNull(10) ? null : reader.GetString(10),
            RevisedEnd = reader.IsDBNull(11) ? null : reader.GetString(11),
            ActualStart = reader.IsDBNull(12) ? null : reader.GetString(12),
            ActualEnd = reader.IsDBNull(13) ? null : reader.GetString(13),
            ProposalStart = reader.IsDBNull(14) ? null : reader.GetString(14),
            ProposalEnd = reader.IsDBNull(15) ? null : reader.GetString(15),
            Status = reader.GetString(16),
            Note = reader.GetString(17),
            SortOrder = reader.GetInt32(18)
        };
        record.PredecessorTaskIds.AddRange(LoadDependencies(connection, record.ScenarioId, record.Id));
        record.SuccessorTaskIds.AddRange(LoadSuccessors(connection, record.ScenarioId, record.Id));
        return record;
    }

    public static void SaveWorkTask(WorkTaskEditRecord record)
    {
        EnsureDatabase();

        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        if (record.SortOrder <= 0)
        {
            record.SortOrder = NextSortOrder(connection, record.ScenarioId);
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO work_tasks (
                id, scenario_id, name, worker_id, difficulty, planned_workload, actual_workload, output_unit,
                baseline_start, baseline_end, revised_start, revised_end, actual_start, actual_end,
                proposal_start, proposal_end, status, note, sort_order
            )
            VALUES (
                $id, $scenario_id, $name, $worker_id, $difficulty, $planned_workload, $actual_workload, $output_unit,
                $baseline_start, $baseline_end, $revised_start, $revised_end, $actual_start, $actual_end,
                $proposal_start, $proposal_end, $status, $note, $sort_order
            )
            ON CONFLICT(id) DO UPDATE SET
                scenario_id = excluded.scenario_id,
                name = excluded.name,
                worker_id = excluded.worker_id,
                difficulty = excluded.difficulty,
                planned_workload = excluded.planned_workload,
                actual_workload = excluded.actual_workload,
                output_unit = excluded.output_unit,
                baseline_start = excluded.baseline_start,
                baseline_end = excluded.baseline_end,
                revised_start = excluded.revised_start,
                revised_end = excluded.revised_end,
                actual_start = excluded.actual_start,
                actual_end = excluded.actual_end,
                proposal_start = excluded.proposal_start,
                proposal_end = excluded.proposal_end,
                status = excluded.status,
                note = excluded.note,
                sort_order = excluded.sort_order;
            """;
        AddParameter(command, "$id", record.Id);
        AddParameter(command, "$scenario_id", record.ScenarioId);
        AddParameter(command, "$name", record.Name);
        AddParameter(command, "$worker_id", record.WorkerId);
        AddParameter(command, "$difficulty", record.Difficulty);
        AddParameter(command, "$planned_workload", record.PlannedWorkload);
        AddParameter(command, "$actual_workload", record.ActualWorkload);
        AddParameter(command, "$output_unit", record.OutputUnit);
        AddParameter(command, "$baseline_start", record.BaselineStart);
        AddParameter(command, "$baseline_end", record.BaselineEnd);
        AddParameter(command, "$revised_start", record.RevisedStart);
        AddParameter(command, "$revised_end", record.RevisedEnd);
        AddParameter(command, "$actual_start", record.ActualStart);
        AddParameter(command, "$actual_end", record.ActualEnd);
        AddParameter(command, "$proposal_start", record.ProposalStart);
        AddParameter(command, "$proposal_end", record.ProposalEnd);
        AddParameter(command, "$status", record.Status);
        AddParameter(command, "$note", record.Note);
        AddParameter(command, "$sort_order", record.SortOrder);
        command.ExecuteNonQuery();

        SaveTaskDependencies(connection, record);
        ClearSurplusRescheduleDates(connection, record);
    }

    private static List<WorkTask> LoadTasks(SqliteConnection connection, string scenarioId)
    {
        var tasks = new List<WorkTask>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                t.id,
                t.name,
                w.name AS worker_name,
                t.difficulty,
                t.planned_workload,
                t.actual_workload,
                t.output_unit,
                t.baseline_start,
                t.baseline_end,
                t.revised_start,
                t.revised_end,
                t.actual_start,
                t.actual_end,
                t.proposal_start,
                t.proposal_end,
                t.status,
                t.note,
                t.worker_id
            FROM work_tasks t
            INNER JOIN workers w ON w.id = t.worker_id
            WHERE t.scenario_id = $scenario_id
            ORDER BY t.sort_order, t.id;
            """;
        command.Parameters.AddWithValue("$scenario_id", scenarioId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var task = new WorkTask
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Worker = reader.GetString(2),
                Difficulty = reader.GetInt32(3),
                PlannedWorkload = Convert.ToDecimal(reader.GetDouble(4)),
                ActualWorkload = Convert.ToDecimal(reader.GetDouble(5)),
                OutputUnit = reader.GetString(6),
                Baseline = [reader.GetString(7), reader.GetString(8)],
                Revised = NullableRange(reader, 9, 10),
                Actual = NullableRange(reader, 11, 12),
                Proposal = NullableRange(reader, 13, 14),
                Status = reader.GetString(15),
                Note = reader.GetString(16),
                WorkerId = reader.GetString(17)
            };
            task.DependsOn.AddRange(LoadDependencies(connection, scenarioId, task.Id));
            tasks.Add(task);
        }

        return tasks;
    }

    private static int NextSortOrder(SqliteConnection connection, string scenarioId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(MAX(sort_order), 0) + 1
            FROM work_tasks
            WHERE scenario_id = $scenario_id;
            """;
        command.Parameters.AddWithValue("$scenario_id", scenarioId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int NextWorkerSortOrder(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(MAX(sort_order), 0) + 1
            FROM workers;
            """;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    public static void UpdateTaskDates(string taskId, string kind, string startDate, string endDate)
    {
        // ドラッグ操作で日付のみを更新する際に使用。
        // 依存関係情報は変更しないため、外部キー制約の問題を回避する。
        EnsureDatabase();

        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        var columnPrefix = kind switch
        {
            "Baseline" => "baseline",
            "Revised" => "revised",
            "Actual" => "actual",
            "Proposal" => "proposal",
            _ => "baseline"
        };

        command.CommandText = $"""
            UPDATE work_tasks
            SET {columnPrefix}_start = $start_date, {columnPrefix}_end = $end_date
            WHERE id = $task_id;
            """;
        command.Parameters.AddWithValue("$task_id", taskId);
        command.Parameters.AddWithValue("$start_date", startDate);
        command.Parameters.AddWithValue("$end_date", endDate);
        command.ExecuteNonQuery();

        // ドラッグによる日付更新後、クリア条件を満たす場合は後続/先行タスクのリスケ・提案をクリアする
        if (kind == "Actual" && !string.IsNullOrWhiteSpace(startDate) && !string.IsNullOrWhiteSpace(endDate))
        {
            ClearSurplusForSuccessors(connection, taskId, endDate);
        }
        else if (kind == "Baseline" && !string.IsNullOrWhiteSpace(startDate))
        {
            ClearSurplusForPredecessors(connection, taskId, startDate);
        }
    }

    /// <summary>
    /// 先行タスクの実績完了日が後続タスクの変更後の当初予定開始日より前になった場合、
    /// 後続タスクのリスケ後予定・再スケ提案をNULLにクリアする。
    /// </summary>
    private static void ClearSurplusRescheduleDates(SqliteConnection connection, WorkTaskEditRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.ActualStart) || string.IsNullOrWhiteSpace(record.ActualEnd))
            return;

        if (!DateTime.TryParseExact(record.ActualEnd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var predecessorActualEnd))
            return;

        var successorIds = LoadSuccessors(connection, record.ScenarioId, record.Id);
        if (successorIds.Count == 0)
            return;

        foreach (var successorId in successorIds)
        {
            using var queryCommand = connection.CreateCommand();
            queryCommand.CommandText = """
                SELECT baseline_start
                FROM work_tasks
                WHERE id = $id;
                """;
            queryCommand.Parameters.AddWithValue("$id", successorId);

            var result = queryCommand.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                continue;

            var baselineStartStr = result as string;
            if (string.IsNullOrWhiteSpace(baselineStartStr))
                continue;

            if (!DateTime.TryParseExact(baselineStartStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var successorBaselineStart))
                continue;

            if (predecessorActualEnd < successorBaselineStart)
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = """
                    UPDATE work_tasks
                    SET revised_start = NULL, revised_end = NULL,
                        proposal_start = NULL, proposal_end = NULL
                    WHERE id = $task_id;
                    """;
                updateCommand.Parameters.AddWithValue("$task_id", successorId);
                updateCommand.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// ドラッグで先行タスクの実績が更新された際、後続タスクのクリア条件をチェックする。
    /// </summary>
    private static void ClearSurplusForSuccessors(SqliteConnection connection, string predecessorTaskId, string actualEndStr)
    {
        if (!DateTime.TryParseExact(actualEndStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var predecessorActualEnd))
            return;

        var scenarioId = GetTaskScenarioId(connection, predecessorTaskId);
        if (scenarioId == null)
            return;

        var successorIds = LoadSuccessors(connection, scenarioId, predecessorTaskId);
        if (successorIds.Count == 0)
            return;

        foreach (var successorId in successorIds)
        {
            using var queryCommand = connection.CreateCommand();
            queryCommand.CommandText = """
                SELECT baseline_start
                FROM work_tasks
                WHERE id = $id;
                """;
            queryCommand.Parameters.AddWithValue("$id", successorId);

            var result = queryCommand.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                continue;

            var baselineStartStr = result as string;
            if (string.IsNullOrWhiteSpace(baselineStartStr))
                continue;

            if (!DateTime.TryParseExact(baselineStartStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var successorBaselineStart))
                continue;

            if (predecessorActualEnd < successorBaselineStart)
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = """
                    UPDATE work_tasks
                    SET revised_start = NULL, revised_end = NULL,
                        proposal_start = NULL, proposal_end = NULL
                    WHERE id = $task_id;
                    """;
                updateCommand.Parameters.AddWithValue("$task_id", successorId);
                updateCommand.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// ドラッグで後続タスクの当初予定が更新された際、先行タスクの実績終了との比較で
    /// 自タスクのリスケ・提案をクリアする。
    /// </summary>
    private static void ClearSurplusForPredecessors(SqliteConnection connection, string successorTaskId, string baselineStartStr)
    {
        if (!DateTime.TryParseExact(baselineStartStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var successorBaselineStart))
            return;

        var scenarioId = GetTaskScenarioId(connection, successorTaskId);
        if (scenarioId == null)
            return;

        var predecessorIds = LoadDependencies(connection, scenarioId, successorTaskId);
        if (predecessorIds.Count == 0)
            return;

        foreach (var predecessorId in predecessorIds)
        {
            using var queryCommand = connection.CreateCommand();
            queryCommand.CommandText = """
                SELECT actual_end
                FROM work_tasks
                WHERE id = $id;
                """;
            queryCommand.Parameters.AddWithValue("$id", predecessorId);

            var result = queryCommand.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                continue;

            var actualEndStr = result as string;
            if (string.IsNullOrWhiteSpace(actualEndStr))
                continue;

            if (!DateTime.TryParseExact(actualEndStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var predecessorActualEnd))
                continue;

            if (predecessorActualEnd < successorBaselineStart)
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = """
                    UPDATE work_tasks
                    SET revised_start = NULL, revised_end = NULL,
                        proposal_start = NULL, proposal_end = NULL
                    WHERE id = $task_id;
                    """;
                updateCommand.Parameters.AddWithValue("$task_id", successorTaskId);
                updateCommand.ExecuteNonQuery();
                break;
            }
        }
    }

    private static string? GetTaskScenarioId(SqliteConnection connection, string taskId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT scenario_id
            FROM work_tasks
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", taskId);
        return command.ExecuteScalar() as string;
    }

    private static void SaveTaskDependencies(SqliteConnection connection, WorkTaskEditRecord record)
    {
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = """
                DELETE FROM task_dependencies
                WHERE task_id = $task_id
                   OR depends_on_task_id = $task_id;
                """;
            deleteCommand.Parameters.AddWithValue("$task_id", record.Id);
            deleteCommand.ExecuteNonQuery();
        }

        var rows = record.PredecessorTaskIds
            .Select((predecessorTaskId, index) => (TaskId: record.Id, DependsOnTaskId: predecessorTaskId, SortOrder: index + 1))
            .Concat(record.SuccessorTaskIds.Select((successorTaskId, index) => (TaskId: successorTaskId, DependsOnTaskId: record.Id, SortOrder: index + 1)))
            .Distinct()
            .ToList();

        foreach (var row in rows)
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = """
                INSERT OR IGNORE INTO task_dependencies (scenario_id, task_id, depends_on_task_id, sort_order)
                VALUES ($scenario_id, $task_id, $depends_on_task_id, $sort_order);
                """;
            insertCommand.Parameters.AddWithValue("$scenario_id", record.ScenarioId);
            insertCommand.Parameters.AddWithValue("$task_id", row.TaskId);
            insertCommand.Parameters.AddWithValue("$depends_on_task_id", row.DependsOnTaskId);
            insertCommand.Parameters.AddWithValue("$sort_order", row.SortOrder);
            insertCommand.ExecuteNonQuery();
        }
    }

    private static List<string> LoadDependencies(SqliteConnection connection, string scenarioId, string taskId)
    {
        var dependencies = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT depends_on_task_id
            FROM task_dependencies
            WHERE scenario_id = $scenario_id
              AND task_id = $task_id
            ORDER BY sort_order, depends_on_task_id;
            """;
        command.Parameters.AddWithValue("$scenario_id", scenarioId);
        command.Parameters.AddWithValue("$task_id", taskId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            dependencies.Add(reader.GetString(0));
        }

        return dependencies;
    }

    private static List<string> LoadSuccessors(SqliteConnection connection, string scenarioId, string taskId)
    {
        var successors = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT task_id
            FROM task_dependencies
            WHERE scenario_id = $scenario_id
              AND depends_on_task_id = $task_id
            ORDER BY sort_order, task_id;
            """;
        command.Parameters.AddWithValue("$scenario_id", scenarioId);
        command.Parameters.AddWithValue("$task_id", taskId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            successors.Add(reader.GetString(0));
        }

        return successors;
    }

    private static string[]? NullableRange(SqliteDataReader reader, int startIndex, int endIndex)
    {
        // 実績や再スケ提案は未入力の状態を許すため、期間全体をnullとして扱う。
        if (reader.IsDBNull(startIndex) || reader.IsDBNull(endIndex))
        {
            return null;
        }

        return [reader.GetString(startIndex), reader.GetString(endIndex)];
    }

    private static void EnsureDatabase()
    {
        var databaseDirectory = Path.GetDirectoryName(DatabasePath)
            ?? throw new InvalidOperationException("Database directory could not be resolved.");
        Directory.CreateDirectory(databaseDirectory);

        // 既存DBがある場合はDBeaver等で編集された内容を尊重し、初期データは再投入しない。
        var shouldSeed = !File.Exists(DatabasePath);

        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        ExecuteSqlScript(connection, "schema.sql");
        if (shouldSeed)
        {
            ExecuteSqlScript(connection, "seed.sql");
        }
    }

    private static void ExecuteSqlScript(SqliteConnection connection, string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", fileName);
        var script = File.ReadAllText(path);

        using var command = connection.CreateCommand();
        command.CommandText = script;
        command.ExecuteNonQuery();
    }
}
