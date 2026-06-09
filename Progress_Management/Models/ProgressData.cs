using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Progress_Management.Models;

// 画面描画用の中間モデル。
// SQLiteのテーブル構造と完全な1対1にはせず、ガント表示で扱いやすい形に寄せている。
public sealed class ProgressScenarioSet
{
    [JsonPropertyName("scenarios")]
    public List<ProgressScenario> Scenarios { get; set; } = [];
}

public sealed class ProgressScenario
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = "";

    [JsonPropertyName("dueStatus")]
    public string DueStatus { get; set; } = "";

    [JsonPropertyName("proposalStatus")]
    public string ProposalStatus { get; set; } = "";

    [JsonPropertyName("tasks")]
    public List<WorkTask> Tasks { get; set; } = [];
}

public sealed class WorkTask
{
    // 予定、実績、再スケ提案の各バーを描くための最小単位。
    // 作業量と難易度は、今後の納期妥当性判定の基礎データとして保持する。
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("worker")]
    public string Worker { get; set; } = "";

    [JsonPropertyName("workerId")]
    public string WorkerId { get; set; } = "";

    [JsonPropertyName("difficulty")]
    public int Difficulty { get; set; }

    [JsonPropertyName("plannedWorkload")]
    public decimal PlannedWorkload { get; set; }

    [JsonPropertyName("actualWorkload")]
    public decimal ActualWorkload { get; set; }

    [JsonPropertyName("outputUnit")]
    public string OutputUnit { get; set; } = "";

    [JsonPropertyName("baseline")]
    public string[] Baseline { get; set; } = [];

    [JsonPropertyName("revised")]
    public string[]? Revised { get; set; }

    [JsonPropertyName("actual")]
    public string[]? Actual { get; set; }

    [JsonPropertyName("proposal")]
    public string[]? Proposal { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("dependsOn")]
    public List<string> DependsOn { get; set; } = [];

    [JsonPropertyName("note")]
    public string Note { get; set; } = "";
}

public enum ChartViewMode
{
    // 個人別工数管理の視点。
    Personal,

    // 先行後続を含むプロジェクト進捗管理の視点。
    Project
}

public enum TimeScale
{
    // 日、週、月の粒度切替。表示密度だけを変え、元データは日単位のまま扱う。
    Day,
    Week,
    Month
}

public enum GantBarKind
{
    // 計画時点の予定。
    Baseline,

    // 現時点でのリスケ後予定。
    Revised,

    // 入力済みの実績。
    Actual,

    // 遅延時などに提示する再スケ案。
    Proposal
}
