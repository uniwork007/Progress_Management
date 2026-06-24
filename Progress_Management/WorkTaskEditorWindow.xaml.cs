using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Progress_Management.Models;
using Progress_Management.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Progress_Management;

public sealed partial class WorkTaskEditorWindow : ContentDialog
{
    private readonly bool _isUpdate;
    private readonly List<LookupItem> _scenarios;
    private readonly List<LookupItem> _workers;

    private static readonly Dictionary<string, string> StatusCodeToDisplay = new()
    {
        { "ontrack", "予定通り" },
        { "delay", "遅延" },
        { "early", "早期" },
        { "proposal", "提案" }
    };

    private static readonly Dictionary<string, string> DisplayToStatusCode = StatusCodeToDisplay
        .ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    public bool WasSaved { get; private set; }

    public string SavedTaskId => TaskIdInput.Text.Trim();

    private TextBlock TitleText => (TextBlock)FindName("TitleTextBlock");

    private TextBlock ModeText => (TextBlock)FindName("ModeTextBlock");

    private TextBox TaskIdInput => (TextBox)FindName("IdTextBox");

    private ComboBox ScenarioInput => (ComboBox)FindName("ScenarioComboBox");

    private TextBox TaskNameInput => (TextBox)FindName("NameTextBox");

    private ComboBox WorkerInput => (ComboBox)FindName("WorkerComboBox");

    private NumberBox DifficultyInput => (NumberBox)FindName("DifficultyNumberBox");

    private NumberBox PlannedWorkloadInput => (NumberBox)FindName("PlannedWorkloadNumberBox");

    private NumberBox ActualWorkloadInput => (NumberBox)FindName("ActualWorkloadNumberBox");

    private TextBox OutputUnitInput => (TextBox)FindName("OutputUnitTextBox");

    private CalendarDatePicker BaselineStartInput => (CalendarDatePicker)FindName("BaselineStartPicker");

    private TextBox BaselineStartText => (TextBox)FindName("BaselineStartTextBox");

    private CalendarDatePicker BaselineEndInput => (CalendarDatePicker)FindName("BaselineEndPicker");

    private TextBox BaselineEndText => (TextBox)FindName("BaselineEndTextBox");

    private CalendarDatePicker RevisedStartInput => (CalendarDatePicker)FindName("RevisedStartPicker");

    private TextBox RevisedStartText => (TextBox)FindName("RevisedStartTextBox");

    private CalendarDatePicker RevisedEndInput => (CalendarDatePicker)FindName("RevisedEndPicker");

    private TextBox RevisedEndText => (TextBox)FindName("RevisedEndTextBox");

    private CalendarDatePicker ActualStartInput => (CalendarDatePicker)FindName("ActualStartPicker");

    private TextBox ActualStartText => (TextBox)FindName("ActualStartTextBox");

    private CalendarDatePicker ActualEndInput => (CalendarDatePicker)FindName("ActualEndPicker");

    private TextBox ActualEndText => (TextBox)FindName("ActualEndTextBox");

    private CalendarDatePicker ProposalStartInput => (CalendarDatePicker)FindName("ProposalStartPicker");

    private TextBox ProposalStartText => (TextBox)FindName("ProposalStartTextBox");

    private CalendarDatePicker ProposalEndInput => (CalendarDatePicker)FindName("ProposalEndPicker");

    private TextBox ProposalEndText => (TextBox)FindName("ProposalEndTextBox");

    private CheckBox RevisedScheduleOptionalInput => (CheckBox)FindName("RevisedScheduleOptionalCheckBox");

    private ComboBox StatusInput => (ComboBox)FindName("StatusComboBox");

    private TextBox PredecessorTaskIdsInput => (TextBox)FindName("PredecessorTaskIdsTextBox");

    private TextBox SuccessorTaskIdsInput => (TextBox)FindName("SuccessorTaskIdsTextBox");

    private NumberBox SortOrderInput => (NumberBox)FindName("SortOrderNumberBox");

    private TextBox NoteInput => (TextBox)FindName("NoteTextBox");

    private TextBlock MessageText => (TextBlock)FindName("MessageTextBlock");

    public WorkTaskEditorWindow(string taskId, string currentScenarioId, bool createNew)
    {
        InitializeComponent();

        var requestedTaskId = taskId.Trim();
        _scenarios = ScenarioRepository.LoadScenarioOptions();
        _workers = ScenarioRepository.LoadWorkerOptions();

        ScenarioInput.ItemsSource = _scenarios;
        ScenarioInput.DisplayMemberPath = nameof(LookupItem.Name);
        WorkerInput.ItemsSource = _workers;
        WorkerInput.DisplayMemberPath = nameof(LookupItem.Name);
        StatusInput.ItemsSource = StatusCodeToDisplay.Values.ToList();

        var existing = string.IsNullOrWhiteSpace(requestedTaskId)
            ? null
            : ScenarioRepository.LoadWorkTaskForEdit(requestedTaskId);

        _isUpdate = !createNew;
        if (!createNew && existing is null)
        {
            throw new InvalidOperationException($"Work task '{requestedTaskId}' was not found.");
        }

        LoadRecord(createNew ? CreateNewRecord(currentScenarioId) : existing!);
        TaskIdInput.IsEnabled = !_isUpdate;

        // リスケ後予定の「省略可」を常に有効にし、新規作成時はデフォルトでチェック
        RevisedScheduleOptionalInput.IsEnabled = true;
        if (createNew)
        {
            RevisedScheduleOptionalInput.IsChecked = true;
        }

        TitleText.Text = _isUpdate ? "作業タスク入力 - 更新" : "作業タスク入力 - 新規";
        ModeText.Text = _isUpdate
            ? $"ID「{requestedTaskId}」は登録済みです。更新として開いています。"
            : "新規入力として開いています。作業IDを含めて入力してください。";
    }

    private static WorkTaskEditRecord CreateNewRecord(string currentScenarioId)
    {
        return new WorkTaskEditRecord
        {
            ScenarioId = currentScenarioId
        };
    }

    private void LoadRecord(WorkTaskEditRecord record)
    {
        TaskIdInput.Text = record.Id;
        SelectLookupItem(ScenarioInput, _scenarios, record.ScenarioId);
        TaskNameInput.Text = record.Name;
        SelectLookupItem(WorkerInput, _workers, record.WorkerId);
        DifficultyInput.Value = record.Difficulty > 0 ? record.Difficulty : double.NaN;
        PlannedWorkloadInput.Value = record.PlannedWorkload > 0 ? Convert.ToDouble(record.PlannedWorkload) : double.NaN;
        ActualWorkloadInput.Value = record.ActualWorkload > 0 ? Convert.ToDouble(record.ActualWorkload) : double.NaN;
        OutputUnitInput.Text = record.OutputUnit;

        LoadDatePickerPair(BaselineStartInput, BaselineStartText, record.BaselineStart);
        LoadDatePickerPair(BaselineEndInput, BaselineEndText, record.BaselineEnd);
        LoadDatePickerPair(RevisedStartInput, RevisedStartText, record.RevisedStart);
        LoadDatePickerPair(RevisedEndInput, RevisedEndText, record.RevisedEnd);
        RevisedScheduleOptionalInput.IsChecked = string.IsNullOrWhiteSpace(record.RevisedStart);
        LoadDatePickerPair(ActualStartInput, ActualStartText, record.ActualStart);
        LoadDatePickerPair(ActualEndInput, ActualEndText, record.ActualEnd);
        LoadDatePickerPair(ProposalStartInput, ProposalStartText, record.ProposalStart);
        LoadDatePickerPair(ProposalEndInput, ProposalEndText, record.ProposalEnd);

        StatusInput.SelectedItem = string.IsNullOrWhiteSpace(record.Status) ? null :
            StatusCodeToDisplay.TryGetValue(record.Status, out var display) ? display : null;
        PredecessorTaskIdsInput.Text = string.Join(", ", record.PredecessorTaskIds);
        SuccessorTaskIdsInput.Text = string.Join(", ", record.SuccessorTaskIds);
        SortOrderInput.Value = record.SortOrder > 0 ? record.SortOrder : double.NaN;
        NoteInput.Text = record.Note;
    }

    private static DateTimeOffset? ToPickerDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return new DateTimeOffset(dt);
        }

        return null;
    }

    private static void SelectLookupItem(ComboBox comboBox, IReadOnlyList<LookupItem> items, string id)
    {
        comboBox.SelectedItem = string.IsNullOrWhiteSpace(id)
            ? null
            : items.FirstOrDefault(item => item.Id == id);
    }

    private static void LoadDatePickerPair(CalendarDatePicker picker, TextBox textBox, string? dateString)
    {
        var date = ToPickerDate(dateString);
        picker.Date = date;
        textBox.Text = date.HasValue ? date.Value.Date.ToString("yyyy/MM/dd") : "";
    }

    private void OnDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        // 対応する TextBox を見つけて更新
        UpdateTextBoxForPicker(sender);
    }

    private void UpdateTextBoxForPicker(CalendarDatePicker picker)
    {
        TextBox? textBox = picker.Name switch
        {
            "BaselineStartPicker" => BaselineStartText,
            "BaselineEndPicker" => BaselineEndText,
            "RevisedStartPicker" => RevisedStartText,
            "RevisedEndPicker" => RevisedEndText,
            "ActualStartPicker" => ActualStartText,
            "ActualEndPicker" => ActualEndText,
            "ProposalStartPicker" => ProposalStartText,
            "ProposalEndPicker" => ProposalEndText,
            _ => null
        };

        if (textBox != null)
        {
            textBox.Text = picker.Date.HasValue ? picker.Date.Value.Date.ToString("yyyy/MM/dd") : "";
        }
    }

    private void OnRevisedScheduleOptionalChanged(object sender, RoutedEventArgs e)
    {
        bool isOptional = RevisedScheduleOptionalInput.IsChecked == true;

        // チェック時はリスケ後予定入力フィールドを無効化し、クリア
        RevisedStartInput.IsEnabled = !isOptional;
        RevisedEndInput.IsEnabled = !isOptional;

        if (isOptional)
        {
            RevisedStartInput.Date = null;
            RevisedEndInput.Date = null;
            RevisedStartText.Text = "";
            RevisedEndText.Text = "";
        }
    }

    private void SaveButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        MessageText.Text = "";

        if (!TryBuildRecord(out var record))
        {
            args.Cancel = true;
            return;
        }

        ScenarioRepository.SaveWorkTask(record);
        WasSaved = true;
    }

    private bool TryBuildRecord(out WorkTaskEditRecord record)
    {
        record = new WorkTaskEditRecord();

        var id = TaskIdInput.Text.Trim();
        var name = TaskNameInput.Text.Trim();
        var outputUnit = OutputUnitInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            MessageText.Text = "作業IDと作業名は必須です。";
            return false;
        }

        if (ScenarioInput.SelectedItem is not LookupItem scenario ||
            WorkerInput.SelectedItem is not LookupItem worker ||
            StatusInput.SelectedItem is not string status)
        {
            MessageText.Text = "シナリオ、担当者、状態を選択してください。";
            return false;
        }

        if (double.IsNaN(DifficultyInput.Value) ||
            double.IsNaN(PlannedWorkloadInput.Value) ||
            double.IsNaN(ActualWorkloadInput.Value))
        {
            MessageText.Text = "難易度、予定作業量、実績作業量を入力してください。";
            return false;
        }

        if (!BaselineStartInput.Date.HasValue || !BaselineEndInput.Date.HasValue)
        {
            MessageText.Text = "当初予定は開始日・終了日の両方をカレンダーから選択してください。";
            return false;
        }

        if (!IsOptionalPickerPair(RevisedStartInput, RevisedEndInput) ||
            !IsOptionalPickerPair(ActualStartInput, ActualEndInput) ||
            !IsOptionalPickerPair(ProposalStartInput, ProposalEndInput))
        {
            MessageText.Text = "リスケ後予定、実績、再スケ提案は開始日・終了日の両方を空欄、または両方をカレンダーから選択してください。";
            return false;
        }

        var predecessorTaskIds = SplitTaskIds(PredecessorTaskIdsInput.Text);
        var successorTaskIds = SplitTaskIds(SuccessorTaskIdsInput.Text);
        if (!ValidateRelationIds(id, scenario.Id, predecessorTaskIds, successorTaskIds))
        {
            return false;
        }

        var statusCode = DisplayToStatusCode.TryGetValue(status, out var code) ? code : "";

        record = new WorkTaskEditRecord
        {
            Id = id,
            ScenarioId = scenario.Id,
            Name = name,
            WorkerId = worker.Id,
            Difficulty = Convert.ToInt32(DifficultyInput.Value),
            PlannedWorkload = Convert.ToDecimal(PlannedWorkloadInput.Value),
            ActualWorkload = Convert.ToDecimal(ActualWorkloadInput.Value),
            OutputUnit = outputUnit,
            BaselineStart = ToStorageDate(BaselineStartInput),
            BaselineEnd = ToStorageDate(BaselineEndInput),
            RevisedStart = ToOptionalStorageDate(RevisedStartInput),
            RevisedEnd = ToOptionalStorageDate(RevisedEndInput),
            ActualStart = ToOptionalStorageDate(ActualStartInput),
            ActualEnd = ToOptionalStorageDate(ActualEndInput),
            ProposalStart = ToOptionalStorageDate(ProposalStartInput),
            ProposalEnd = ToOptionalStorageDate(ProposalEndInput),
            Status = statusCode,
            PredecessorTaskIds = predecessorTaskIds,
            SuccessorTaskIds = successorTaskIds,
            SortOrder = double.IsNaN(SortOrderInput.Value) ? 0 : Convert.ToInt32(SortOrderInput.Value),
            Note = NoteInput.Text
        };
        return true;
    }

    private bool ValidateRelationIds(string currentTaskId, string scenarioId, IReadOnlyList<string> predecessorTaskIds, IReadOnlyList<string> successorTaskIds)
    {
        // 全シナリオのタスクIDを既知IDとして使用（task_dependenciesのFKはwork_tasks.idをグローバル参照するため）
        var knownTaskIds = ScenarioRepository.LoadAllTaskIds().ToHashSet(StringComparer.OrdinalIgnoreCase);
        knownTaskIds.Add(currentTaskId);

        if (predecessorTaskIds.Contains(currentTaskId, StringComparer.OrdinalIgnoreCase) ||
            successorTaskIds.Contains(currentTaskId, StringComparer.OrdinalIgnoreCase))
        {
            MessageText.Text = "先行タスクまたは後続タスクに、自分自身の作業IDは指定できません。";
            return false;
        }

        var unknownTaskIds = predecessorTaskIds.Concat(successorTaskIds)
            .Where(taskId => !knownTaskIds.Contains(taskId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unknownTaskIds.Count > 0)
        {
            MessageText.Text = $"存在しない作業IDがあります: {string.Join(", ", unknownTaskIds)}";
            return false;
        }

        var duplicatedBothSides = predecessorTaskIds.Intersect(successorTaskIds, StringComparer.OrdinalIgnoreCase).ToList();
        if (duplicatedBothSides.Count > 0)
        {
            MessageText.Text = $"同じ作業IDを先行と後続の両方に指定できません: {string.Join(", ", duplicatedBothSides)}";
            return false;
        }

        return true;
    }

    private static List<string> SplitTaskIds(string value)
    {
        return value
            .Split([',', '、', ';', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsOptionalPickerPair(CalendarDatePicker start, CalendarDatePicker end)
    {
        return start.Date.HasValue == end.Date.HasValue;
    }

    private static string ToStorageDate(CalendarDatePicker picker)
    {
        return picker.Date!.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string? ToOptionalStorageDate(CalendarDatePicker picker)
    {
        return picker.Date.HasValue
            ? picker.Date.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    private void CancelButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        WasSaved = false;
    }
}
