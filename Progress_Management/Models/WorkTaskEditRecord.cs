using System.Collections.Generic;

namespace Progress_Management.Models;

public sealed class LookupItem
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public override string ToString() => Name;
}

public sealed class WorkerEditRecord
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public int SortOrder { get; set; }
}

public sealed class WorkTaskEditRecord
{
    public string Id { get; set; } = "";

    public string ScenarioId { get; set; } = "";

    public string Name { get; set; } = "";

    public string WorkerId { get; set; } = "";

    public int Difficulty { get; set; }

    public decimal PlannedWorkload { get; set; }

    public decimal ActualWorkload { get; set; }

    public string OutputUnit { get; set; } = "";

    public string BaselineStart { get; set; } = "";

    public string BaselineEnd { get; set; } = "";

    public string? RevisedStart { get; set; }

    public string? RevisedEnd { get; set; }

    public string? ActualStart { get; set; }

    public string? ActualEnd { get; set; }

    public string? ProposalStart { get; set; }

    public string? ProposalEnd { get; set; }

    public string Status { get; set; } = "";

    public List<string> PredecessorTaskIds { get; set; } = [];

    public List<string> SuccessorTaskIds { get; set; } = [];

    public string Note { get; set; } = "";

    public int SortOrder { get; set; }
}
