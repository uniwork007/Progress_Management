using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Progress_Management.Models;
using Progress_Management.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace Progress_Management;

public sealed partial class MainWindow : Window
{
    // 自動生成ファイルで定義される x:Name 要素にアクセスするため partial を利用します。
    // ビルド環境で .g.cs が生成されている想定のため、ここではフィールドを追加しません。
    // ガント描画の基準寸法。将来専用コントロール化するまではここで見た目を調整する。
    private const double LabelWidth = 188;
    private const double RowHeight = 92;
    private const double BarHeight = 22;

    private ProgressScenarioSet _scenarioSet;
    private ProgressScenario _currentScenario;
    private WorkTask? _selectedTask;
    private string _selectedKind = "作業";
    private ChartViewMode _viewMode = ChartViewMode.Personal;
    private TimeScale _timeScale = TimeScale.Day;
    // ComboBox初期化中に発生するSelectionChangedで再描画しないためのガード。
    private bool _isBinding;
    // 全タスク表示かどうかのフラグ
    private bool _isShowingAllTasks;

    public MainWindow()
    {
        InitializeComponent();
        _scenarioSet = ScenarioRepository.Load() ?? new ProgressScenarioSet();
        _currentScenario = _scenarioSet.Scenarios.FirstOrDefault() ?? new ProgressScenario { Name = "新規シナリオ", Id = "new" };
        
        if (_scenarioSet.Scenarios.Count == 0)
        {
            _scenarioSet.Scenarios.Add(_currentScenario);
        }

        BindControls();
        // 起動時は全タスク表示を初期化
        _isShowingAllTasks = true;
        _currentScenario = CreateAllTasksScenario();
        Render();
    }

    private void BindControls()
    {
        _isBinding = true;
        var scenarioNames = new List<string> { "【全タスク】" };
        scenarioNames.AddRange(_scenarioSet.Scenarios.Select(s => s.Name));
        ScenarioComboBox.ItemsSource = scenarioNames;
        ScenarioComboBox.SelectedIndex = 0;
        ViewModeComboBox.ItemsSource = new[] { "個人", "プロジェクト" };
        ViewModeComboBox.SelectedIndex = 0;
        ScaleComboBox.ItemsSource = new[] { "日", "週", "月" };
        ScaleComboBox.SelectedIndex = 0;
        _isBinding = false;
    }

    private void ScenarioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBinding || ScenarioComboBox.SelectedIndex < 0) return;
        
        if (ScenarioComboBox.SelectedIndex == 0)
        {
            // 全タスク表示
            _isShowingAllTasks = true;
            _currentScenario = CreateAllTasksScenario();
        }
        else
        {
            // 個別シナリオ表示
            _isShowingAllTasks = false;
            _currentScenario = _scenarioSet.Scenarios[ScenarioComboBox.SelectedIndex - 1];
        }
        
        _selectedTask = null;
        _selectedKind = "作業";
        Render();
    }

    private ProgressScenario CreateAllTasksScenario()
    {
        var allTasks = _scenarioSet.Scenarios
            .SelectMany(s => s.Tasks)
            .OrderBy(t => t.Id)
            .ToList();

        return new ProgressScenario
        {
            Id = "all-tasks",
            Name = "【全タスク】",
            Purpose = "全シナリオ統合表示",
            DueStatus = "",
            ProposalStatus = "",
            Tasks = allTasks
        };
    }

    private void ViewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBinding) return;
        _viewMode = ViewModeComboBox.SelectedIndex == 1 ? ChartViewMode.Project : ChartViewMode.Personal;
        Render();
    }

    private void ScaleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBinding) return;
        _timeScale = ScaleComboBox.SelectedIndex switch
        {
            1 => TimeScale.Week,
            2 => TimeScale.Month,
            _ => TimeScale.Day
        };
        Render();
    }

    private void TaskListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TaskListView.SelectedItem is not ListViewItem { Tag: WorkTask task }) return;
        _selectedTask = task;
        _selectedKind = "作業";
        RenderDetail();
    }

    private async void OpenTaskEditorButton_Click(object sender, RoutedEventArgs e)
    {
        var taskId = TaskIdTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        if (ScenarioRepository.LoadWorkTaskForEdit(taskId) is null)
        {
            TaskCommandMessageTextBlock.Text = $"ID「{taskId}」は未登録です。新規作成は「新規」ボタンを使用してください。";
            return;
        }

        await OpenTaskEditor(taskId);
    }

    private void Render()
    {
        // シナリオ、表示モード、時間軸を変更したときの再描画入口。
        _selectedTask ??= _currentScenario.Tasks.FirstOrDefault();
        PurposeTextBlock.Text = _currentScenario.Purpose;
        DueStatusTextBlock.Text = _currentScenario.DueStatus;
        ProposalStatusTextBlock.Text = _currentScenario.ProposalStatus;
        TaskCountTextBlock.Text = $"{_currentScenario.Tasks.Count}件";

        RenderTaskList();
        RenderChart();
        RenderDetail();
    }

    private void RenderTaskList()
    {
        TaskListView.Items.Clear();
        foreach (var task in _currentScenario.Tasks)
        {
            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock { Text = task.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            panel.Children.Add(new TextBlock
            {
                Text = $"{task.Worker} / {StatusLabel(task.Status)}",
                Foreground = Brush("#657168"),
                FontSize = 12
            });

            var item = new ListViewItem
            {
                Content = panel,
                Tag = task,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6),
                IsSelected = ReferenceEquals(task, _selectedTask)
            };
            TaskListView.Items.Add(item);
        }
    }

    private void RenderChart()
    {
        // DBから読み込んだ業務モデルを、画面上の行・バー・依存線へ展開する。
        ChartStackPanel.Children.Clear();
        if (_currentScenario.Tasks.Count == 0) return;

        // 表示対象期間は、当初予定・リスケ後予定・実績・提案の全日付から自動算出する。
        var extent = GetExtent(_currentScenario.Tasks);
        var step = ScaleStep();
        var unitWidth = UnitWidth();
        var tickCount = Math.Max(1, (int)Math.Ceiling((extent.End - extent.Start).TotalDays / step) + 1);
        var chartWidth = tickCount * unitWidth;

        ChartStackPanel.Children.Add(BuildTimelineHeader(extent, tickCount, unitWidth, step));

        var taskById = _currentScenario.Tasks.Select((task, index) => (Task: task, Index: index))
            .ToDictionary(x => x.Task.Id, x => x);

        foreach (var row in DisplayRows())
        {
            var grid = new Grid { Height = RowHeight, MinWidth = LabelWidth + chartWidth };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(chartWidth) });

            var label = BuildRowLabel(row.Label, row.SubLabel);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var canvas = new Canvas
            {
                Width = chartWidth,
                Height = RowHeight,
                Background = Brush("#FBFCFA")
            };

            DrawGridLines(canvas, tickCount, unitWidth);
            AddTaskBars(canvas, row.Task, extent, unitWidth, step);

            if (_viewMode == ChartViewMode.Project)
            {
                // プロジェクト表示では、当初予定超過の警告ラインと、作業間の先行後続関係を表示する。
                AddAlertMarker(canvas, row.Task, extent, unitWidth, step);
                DrawDependencies(canvas, row.Task, taskById, extent, unitWidth, step);
            }
            else
            {
                // 個人表示（ChartViewMode.Personal）の時のみ限定で、
                // 同一担当者の時系列順タスク間で「先行後続のような関係」のラインを描画し、
                // 予実日付から逆転している場合に警告を表示する。
                var workerTasks = _currentScenario.Tasks
                    .Where(t => t.WorkerId == row.Task.WorkerId)
                    .OrderBy(t => PlannedStart(t) ?? DateTime.MinValue)
                    .ToList();
                DrawPersonalDependencies(canvas, row.Task, workerTasks, extent, unitWidth, step);
            }

            Grid.SetColumn(canvas, 1);
            grid.Children.Add(canvas);
            ChartStackPanel.Children.Add(grid);
        }
    }

    private Grid BuildTimelineHeader((DateTime Start, DateTime End) extent, int tickCount, double unitWidth, int step)
    {
        var header = new Grid { Height = 40, MinWidth = LabelWidth + tickCount * unitWidth };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(tickCount * unitWidth) });

        var corner = new Border
        {
            BorderBrush = Brush("#D7DDD4"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = new TextBlock
            {
                Text = $"{extent.Start:yyyy年M月}",
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("#657168"),
                FontSize = 12
            }
        };
        header.Children.Add(corner);

        var timeline = new Canvas { Width = tickCount * unitWidth, Height = 40 };
        for (var i = 0; i < tickCount; i++)
        {
            var tickDate = extent.Start.AddDays(i * step);
            var tick = new TextBlock
            {
                Text = ScaleLabel(tickDate),
                FontSize = 11,
                Foreground = Brush("#657168")
            };
            Canvas.SetLeft(tick, i * unitWidth + 8);
            Canvas.SetTop(tick, 12);
            timeline.Children.Add(tick);
        }
        Grid.SetColumn(timeline, 1);
        header.Children.Add(timeline);
        return header;
    }

    private static Border BuildRowLabel(string label, string subLabel)
    {
        var panel = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 });
        panel.Children.Add(new TextBlock { Text = subLabel, Foreground = Brush("#657168"), FontSize = 11 });
        return new Border
        {
            BorderBrush = Brush("#D7DDD4"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(12, 0, 12, 0),
            Child = panel
        };
    }

    private void AddTaskBars(Canvas canvas, WorkTask task, (DateTime Start, DateTime End) extent, double unitWidth, int step)
    {
        // 1つの作業に対し、当初予定・リスケ後予定または提案・実績を別バーとして描く。
        if (IsValidDateRange(task.Baseline))
        {
            AddBar(canvas, task, GantBarKind.Baseline, task.Baseline, "当初", 12, "#58718D", extent, unitWidth, step);
        }
        
        if (task.Proposal is not null && IsValidDateRange(task.Proposal))
        {
            // 提案がある場合は提案を表示
            AddBar(canvas, task, GantBarKind.Proposal, task.Proposal, "提案", 38, "#8B6FBB", extent, unitWidth, step);
        }
        else if (task.Revised is not null && IsValidDateRange(task.Revised))
        {
            // 提案がない場合はリスケ後予定を表示
            AddBar(canvas, task, GantBarKind.Revised, task.Revised, "リスケ", 38, "#2F8B72", extent, unitWidth, step);
        }
        
        // 実績は日付が有効な場合のみ表示
        if (task.Actual is not null && IsValidDateRange(task.Actual))
        {
            AddBar(canvas, task, GantBarKind.Actual, task.Actual, "実績", 64, "#D3833F", extent, unitWidth, step);
        }
    }

    private static bool IsValidDateRange(string[]? range)
    {
        if (range == null || range.Length < 2)
            return false;
        
        // 開始日と終了日が共に有効な日付フォーマットか確認
        var startValid = DateTime.TryParseExact(range[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        var endValid = DateTime.TryParseExact(range[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        
        return startValid && endValid;
    }

    private void AddBar(Canvas canvas, WorkTask task, GantBarKind kind, string[]? range, string label, double top, string color, (DateTime Start, DateTime End) extent, double unitWidth, int step)
    {
        if (range == null || range.Length < 2) return;

        var left = ScaledLeft(extent.Start, range[0], unitWidth, step);
        var width = ScaledWidth(range, unitWidth, step);
        var border = new Border
        {
            Width = width,
            Height = BarHeight,
            CornerRadius = new CornerRadius(4),
            Background = Brush(color),
            BorderBrush = Brush(task.Status == "delay" && (kind == GantBarKind.Actual || kind == GantBarKind.Proposal) ? "#C94949" : "#26352D"),
            BorderThickness = new Thickness(task.Status == "delay" && (kind == GantBarKind.Actual || kind == GantBarKind.Proposal) ? 2 : 1),
            Child = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 11,
                Margin = new Thickness(7, 2, 7, 0)
            },
            Tag = (Task: task, Kind: kind)
        };
        border.Tapped += Bar_Tapped;
        border.DoubleTapped += Bar_DoubleTapped;

        Canvas.SetLeft(border, left);
        Canvas.SetTop(border, top);
        canvas.Children.Add(border);
    }

    private void Bar_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Border { Tag: ValueTuple<WorkTask, GantBarKind> tag }) return;
        _selectedTask = tag.Item1;
        _selectedKind = KindLabel(tag.Item2);
        TaskIdTextBox.Text = tag.Item1.Id;
        RenderDetail();
        e.Handled = true;
    }

    private async void Bar_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not Border { Tag: ValueTuple<WorkTask, GantBarKind> tag }) return;
        _selectedTask = tag.Item1;
        _selectedKind = KindLabel(tag.Item2);
        TaskIdTextBox.Text = tag.Item1.Id;
        RenderDetail();
        await OpenTaskEditor(tag.Item1.Id);
        e.Handled = true;
    }

    private async Task OpenTaskEditor(string taskId)
    {
        var requestedTaskId = taskId.Trim();
        var editor = new WorkTaskEditorWindow(requestedTaskId, _currentScenario.Id, false)
        {
            XamlRoot = GetDialogXamlRoot()
        };

        await editor.ShowAsync();
        if (editor.WasSaved)
        {
            ReloadAfterTaskEdit(editor.SavedTaskId);
        }
    }

    private async void NewTaskEditorButton_Click(object sender, RoutedEventArgs e)
    {
        var editor = new WorkTaskEditorWindow("", _currentScenario.Id, true)
        {
            XamlRoot = GetDialogXamlRoot()
        };

        await editor.ShowAsync();
        if (editor.WasSaved)
        {
            ReloadAfterTaskEdit(editor.SavedTaskId);
        }
    }

    private async void OpenWorkerMaintenanceButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WorkerMaintenanceDialog
        {
            XamlRoot = GetDialogXamlRoot()
        };

        await dialog.ShowAsync();
        if (dialog.WasChanged)
        {
            ReloadAfterWorkerEdit();
        }
    }

    private XamlRoot GetDialogXamlRoot()
    {
        if (Content is FrameworkElement element)
        {
            return element.XamlRoot;
        }

        throw new InvalidOperationException("Dialog XamlRoot could not be resolved.");
    }

    private void ReloadAfterTaskEdit(string taskId)
    {
        var savedRecord = ScenarioRepository.LoadWorkTaskForEdit(taskId);
        var currentScenarioId = savedRecord?.ScenarioId ?? _currentScenario.Id;
        _scenarioSet = ScenarioRepository.Load() ?? new ProgressScenarioSet();
        
        // 全タスク表示の状態を復元
        if (_isShowingAllTasks)
        {
            _currentScenario = CreateAllTasksScenario();
        }
        else
        {
            _currentScenario = _scenarioSet.Scenarios.FirstOrDefault(s => s.Id == currentScenarioId)
                ?? _scenarioSet.Scenarios.FirstOrDefault() ?? _currentScenario;
        }
        
        _selectedTask = _currentScenario.Tasks.FirstOrDefault(task => task.Id == taskId)
            ?? _currentScenario.Tasks.FirstOrDefault();
        BindControls();
        
        // 全タスク表示の場合、インデックス0を選択
        if (_isShowingAllTasks)
        {
            ScenarioComboBox.SelectedIndex = 0;
        }
        else
        {
            ScenarioComboBox.SelectedIndex = _scenarioSet.Scenarios.FindIndex(s => s.Id == _currentScenario.Id) + 1;
        }
        
        TaskIdTextBox.Text = taskId;
        Render();
    }

    private void ReloadAfterWorkerEdit()
    {
        var selectedTaskId = _selectedTask?.Id;

        _scenarioSet = ScenarioRepository.Load() ?? new ProgressScenarioSet();
        
        // 全タスク表示の状態を復元
        if (_isShowingAllTasks)
        {
            _currentScenario = CreateAllTasksScenario();
        }
        else
        {
            // 既存シナリオを復元（存在しない場合は最初のシナリオ）
            _currentScenario = _scenarioSet.Scenarios.FirstOrDefault() ?? _currentScenario;
        }
        
        _selectedTask = string.IsNullOrWhiteSpace(selectedTaskId)
            ? _currentScenario.Tasks.FirstOrDefault()
            : _currentScenario.Tasks.FirstOrDefault(task => task.Id == selectedTaskId)
                ?? _currentScenario.Tasks.FirstOrDefault();

        BindControls();
        
        // 全タスク表示の場合、インデックス0を選択
        if (_isShowingAllTasks)
        {
            ScenarioComboBox.SelectedIndex = 0;
        }
        else
        {
            ScenarioComboBox.SelectedIndex = _scenarioSet.Scenarios.FindIndex(s => s.Id == _currentScenario.Id) + 1;
        }
        
        Render();
    }

    private void AddAlertMarker(Canvas canvas, WorkTask task, (DateTime Start, DateTime End) extent, double unitWidth, int step)
    {
        // 日付ベースで当初予定を超過している作業に、逆転/遅延の視覚マーカーを置く。
        if (task.Status != "delay" || task.Baseline.Length < 2) return;
        var left = ScaledLeft(extent.Start, task.Baseline[1], unitWidth, step) + unitWidth;
        var line = new Line
        {
            X1 = left,
            X2 = left,
            Y1 = 8,
            Y2 = 82,
            Stroke = Brush("#C94949"),
            StrokeThickness = 3
        };
        canvas.Children.Add(line);
        var label = new Border
        {
            Background = Brush("#C94949"),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Child = new TextBlock { Text = "逆転", Foreground = new SolidColorBrush(Colors.White), FontSize = 11 }
        };
        Canvas.SetLeft(label, left + 5);
        Canvas.SetTop(label, 2);
        canvas.Children.Add(label);
    }

    private void DrawDependencies(Canvas canvas, WorkTask task, Dictionary<string, (WorkTask Task, int Index)> taskById, (DateTime Start, DateTime End) extent, double unitWidth, int step)
    {
        if (task.DependsOn.Count == 0) return;

        foreach (var fromId in task.DependsOn)
        {
            if (!taskById.TryGetValue(fromId, out var source)) continue;
            WorkTask fromTask = source.Task;
            var fromRange = fromTask.Actual ?? fromTask.Revised;
            var toRange = task.Proposal ?? task.Revised;
            if (fromRange == null || fromRange.Length < 2 || toRange == null || toRange.Length < 2) continue;
            var startX = ScaledLeft(extent.Start, fromRange[1], unitWidth, step) + unitWidth;
            var endX = ScaledLeft(extent.Start, toRange[0], unitWidth, step);
            
            // 実績終了日と後続タスク当初予定開始日の比較で逆転判定
            bool hasInversion = false;
            if (fromTask.Actual != null && fromTask.Actual.Length >= 2 && task.Baseline != null && task.Baseline.Length >= 2)
            {
                var actualEnd = ParseDate(fromTask.Actual[1]);
                var baselineStart = ParseDate(task.Baseline[0]);
                hasInversion = actualEnd > baselineStart;
            }
            
            var hasWarning = hasInversion || HasDependencyWarning(fromTask.Id, task.Id);

            var line = new Polyline
            {
                Stroke = Brush(hasWarning ? "#C94949" : "#26352D"),
                StrokeThickness = hasWarning ? 3 : 2,
                Points =
                {
                    new Windows.Foundation.Point(startX, 75),
                    new Windows.Foundation.Point(startX + 8, 75),
                    new Windows.Foundation.Point(startX + 8, 49),
                    new Windows.Foundation.Point(endX - 5, 49)
                }
            };
            canvas.Children.Add(line);
        }
    }

    private void DrawPersonalDependencies(Canvas canvas, WorkTask task, List<WorkTask> workerTasks, (DateTime Start, DateTime End) extent, double unitWidth, int step)
    {
        var index = workerTasks.FindIndex(t => t.Id == task.Id);
        if (index <= 0) return;

        var predecessor = workerTasks[index - 1];
        var fromRange = predecessor.Actual ?? predecessor.Revised;
        var toRange = task.Proposal ?? task.Revised;

        if (fromRange == null || fromRange.Length < 2 || toRange == null || toRange.Length < 2) return;

        var startX = ScaledLeft(extent.Start, fromRange[1], unitWidth, step) + unitWidth;
        var endX = ScaledLeft(extent.Start, toRange[0], unitWidth, step);

        // 実績終了日と後続タスク当初予定開始日の比較で逆転判定
        bool hasInversion = false;
        if (predecessor.Actual != null && predecessor.Actual.Length >= 2 && task.Baseline != null && task.Baseline.Length >= 2)
        {
            var actualEnd = ParseDate(predecessor.Actual[1]);
            var baselineStart = ParseDate(task.Baseline[0]);
            hasInversion = actualEnd > baselineStart;
        }

        var line = new Polyline
        {
            Stroke = Brush(hasInversion ? "#C94949" : "#657168"),
            StrokeThickness = hasInversion ? 3 : 2,
            Points =
            {
                new Windows.Foundation.Point(startX, 75),
                new Windows.Foundation.Point(startX + 8, 75),
                new Windows.Foundation.Point(startX + 8, 49),
                new Windows.Foundation.Point(endX - 5, 49)
            }
        };
        canvas.Children.Add(line);

        if (hasInversion)
        {
            // 逆転マーカーを配置
            var left = startX;
            var alertLine = new Line
            {
                X1 = left,
                X2 = left,
                Y1 = 8,
                Y2 = 82,
                Stroke = Brush("#C94949"),
                StrokeThickness = 3
            };
            canvas.Children.Add(alertLine);

            var label = new Border
            {
                Background = Brush("#C94949"),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Child = new TextBlock { Text = "逆転", Foreground = new SolidColorBrush(Colors.White), FontSize = 11 }
            };
            Canvas.SetLeft(label, left + 5);
            Canvas.SetTop(label, 2);
            canvas.Children.Add(label);
        }
    }

    private void DrawGridLines(Canvas canvas, int tickCount, double unitWidth)
    {
        for (var i = 0; i <= tickCount; i++)
        {
            var x = i * unitWidth;
            canvas.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = RowHeight,
                Stroke = Brush("#EDF0EB"),
                StrokeThickness = 1
            });
        }
        canvas.Children.Add(new Line
        {
            X1 = 0,
            X2 = tickCount * unitWidth,
            Y1 = RowHeight - 1,
            Y2 = RowHeight - 1,
            Stroke = Brush("#D7DDD4"),
            StrokeThickness = 1
        });
    }

    private IEnumerable<(string Label, string SubLabel, WorkTask Task)> DisplayRows()
    {
        if (_viewMode == ChartViewMode.Project)
        {
            return _currentScenario.Tasks.Select(task =>
                (task.Name, $"[{task.Id}] {task.Worker} / 難易度 {task.Difficulty}", task));
        }
        else
        {
            // 個人表示では担当者ID(worker.id)順に並び替え
            // 同じ担当者のタスク内では、予定開始日(PlannedStart)順に並べる
            return _currentScenario.Tasks
                .OrderBy(task => task.WorkerId)
                .ThenBy(task => PlannedStart(task) ?? DateTime.MinValue)
                .Select(task => (task.Worker, $"{task.Name} / [{task.Id}]", task));
        }
    }

    private static DateTime? PlannedStart(WorkTask task)
    {
        var range = task.Revised ?? task.Baseline;
        return range != null && range.Length >= 2 ? ParseDate(range[0]) : null;
    }

    private void RenderDetail()
    {
        // ガントバーまたは作業一覧の選択内容を、右側の文字情報として表示する。
        if (_selectedTask is null) return;

        SelectedKindTextBlock.Text = _selectedKind;
        DetailGrid.Children.Clear();
        DetailGrid.RowDefinitions.Clear();

        AddDetailRow("作業ID", _selectedTask.Id);
        AddDetailRow("作業名", _selectedTask.Name);
        AddDetailRow("担当者", _selectedTask.Worker);
        AddDetailRow("状態", StatusLabel(_selectedTask.Status));
        AddDetailRow("当初予定", RangeText(_selectedTask.Baseline));
        AddDetailRow("リスケ後", RangeText(_selectedTask.Revised));
        AddDetailRow("実績", _selectedTask.Actual is null ? "未入力" : RangeText(_selectedTask.Actual));
        AddDetailRow("提案", _selectedTask.Proposal is null ? "なし" : RangeText(_selectedTask.Proposal));
        AddDetailRow("予定量", $"{_selectedTask.PlannedWorkload} {_selectedTask.OutputUnit}");
        AddDetailRow("実績量", $"{_selectedTask.ActualWorkload} {_selectedTask.OutputUnit}");
        AddDetailRow("難易度", _selectedTask.Difficulty.ToString(CultureInfo.InvariantCulture));
        AddDetailRow("先行作業", _selectedTask.DependsOn.Count == 0 ? "なし" : string.Join(", ", _selectedTask.DependsOn));
        AddDetailRow("後続作業", SuccessorText(_selectedTask));
        AddDependencyWarningRows(_selectedTask);
        AddDetailRow("メモ", _selectedTask.Note);
    }

    private void AddDetailRow(string key, string value)
    {
        AddDetailRow(key, value, "#111812");
    }

    private void AddDetailRow(string key, string value, string valueColor)
    {
        var row = DetailGrid.RowDefinitions.Count;
        DetailGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var keyText = new TextBlock
        {
            Text = key,
            Foreground = Brush("#657168"),
            Margin = new Thickness(0, 0, 10, 8)
        };
        Grid.SetRow(keyText, row);
        Grid.SetColumn(keyText, 0);
        DetailGrid.Children.Add(keyText);

        var valueText = new TextBlock
        {
            Text = value,
            Foreground = Brush(valueColor),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(valueText, row);
        Grid.SetColumn(valueText, 1);
        DetailGrid.Children.Add(valueText);
    }

    private string SuccessorText(WorkTask task)
    {
        var successors = _currentScenario.Tasks
            .Where(candidate => candidate.DependsOn.Contains(task.Id))
            .Select(candidate => candidate.Id)
            .ToList();
        return successors.Count == 0 ? "なし" : string.Join(", ", successors);
    }

    private void AddDependencyWarningRows(WorkTask task)
    {
        var warnings = ScheduleWarningService.Analyze(_currentScenario.Tasks)
            .Where(warning => warning.Involves(task.Id))
            .Select(warning => warning.Message)
            .Distinct()
            .ToList();
        if (warnings.Count == 0)
        {
            return;
        }

        AddDetailRow("警告", string.Join(Environment.NewLine, warnings), "#C94949");
    }

    private bool HasDependencyWarning(string predecessorTaskId, string successorTaskId)
    {
        return ScheduleWarningService.Analyze(_currentScenario.Tasks)
            .Any(warning => warning.MatchesDependency(predecessorTaskId, successorTaskId));
    }

    private IEnumerable<string> DependencyWarningsForTask(WorkTask task)
    {
        var taskById = _currentScenario.Tasks.ToDictionary(item => item.Id);

        foreach (var predecessorId in task.DependsOn)
        {
            if (!taskById.TryGetValue(predecessorId, out var predecessor))
            {
                yield return $"先行タスク {predecessorId} が見つかりません。";
                continue;
            }

            var warning = DependencyWarning(predecessor, task);
            if (warning is not null)
            {
                yield return warning;
            }
        }

        foreach (var successor in _currentScenario.Tasks.Where(candidate => candidate.DependsOn.Contains(task.Id)))
        {
            var warning = DependencyWarning(task, successor);
            if (warning is not null)
            {
                yield return warning;
            }
        }
    }

    private static (DateTime Start, DateTime End) GetExtent(IEnumerable<WorkTask> tasks)
    {
        var dates = tasks
            .SelectMany(task => new[] { task.Baseline, task.Revised, task.Actual, task.Proposal })
            .Where(range => range != null && range.Length >= 2)
            .SelectMany(range => range!)
            .Select(d => {
                if (DateTime.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return (DateTime?)dt;
                return null;
            })
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        // 日付データが1つもない場合のデフォルト範囲を設定
        if (dates.Count == 0)
        {
            return (DateTime.Today.AddDays(-7), DateTime.Today.AddDays(7));
        }
        return (dates.Min().AddDays(-1), dates.Max().AddDays(2));
    }

    private int ScaleStep() => _timeScale switch
    {
        TimeScale.Week => 7,
        TimeScale.Month => 30,
        _ => 1
    };

    private double UnitWidth() => _timeScale switch
    {
        TimeScale.Week => 46,
        TimeScale.Month => 68,
        _ => 34
    };

    private string ScaleLabel(DateTime date) => _timeScale switch
    {
        TimeScale.Month => $"{date.Month}月",
        TimeScale.Week => $"{date.Month}/{date.Day}",
        _ => date.Day.ToString(CultureInfo.InvariantCulture)
    };

    private static double ScaledLeft(DateTime extentStart, string date, double unitWidth, int step)
    {
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return 0;
        return Math.Max(0, (dt - extentStart).TotalDays / step * unitWidth);
    }

    private static double ScaledWidth(string[] range, double unitWidth, int step)
    {
        if (!DateTime.TryParseExact(range[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
            !DateTime.TryParseExact(range[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            return 18;

        var days = Math.Max(1, (end - start).TotalDays + 1);
        return Math.Max(18, days / step * unitWidth);
    }

    private static DateTime ParseDate(string value)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.Today; // 解析失敗時のフォールバック
    }


    private static string RangeDisplay(string dateString)
    {
        // ストレージは yyyy-MM-dd で保存される想定のため、表示は yyyy/MM/dd に変換して返す。
        try
        {
            var d = ParseDate(dateString);
            return d.ToString("yyyy/MM/dd", CultureInfo.GetCultureInfo("ja-JP"));
        }
        catch
        {
            return dateString;
        }
    }

    private static string RangeText(string[]? range) => (range == null || range.Length < 2) ? "未入力" : $"{RangeDisplay(range[0])} - {RangeDisplay(range[1])}";

    private static string? DependencyWarning(WorkTask predecessor, WorkTask successor)
    {
        var predecessorRange = predecessor.Actual ?? predecessor.Revised;
        var successorRange = successor.Proposal ?? successor.Revised;
        if (predecessorRange == null || predecessorRange.Length < 2 || successorRange == null || successorRange.Length < 2)
        {
            return null;
        }

        var predecessorEnd = ParseDate(predecessorRange[1]);
        var successorStart = ParseDate(successorRange[0]);
        return predecessorEnd > successorStart
            ? $"先行後続矛盾: {predecessor.Id} の終了日({RangeDisplay(predecessorRange[1])})が {successor.Id} の開始日({RangeDisplay(successorRange[0])})より後です。"
            : null;
    }

    private static string KindLabel(GantBarKind kind) => kind switch
    {
        GantBarKind.Baseline => "当初予定",
        GantBarKind.Revised => "リスケ後予定",
        GantBarKind.Actual => "実績",
        GantBarKind.Proposal => "再スケ提案",
        _ => "作業"
    };

    private static string StatusLabel(string status) => status switch
    {
        "ontrack" => "オンスケジュール",
        "delay" => "遅延/逆転",
        "early" => "予定前完了",
        "proposal" => "提案",
        _ => status
    };

    private static SolidColorBrush Brush(string hex)
    {
        // XAMLではなくコード側で生成する図形用の色ヘルパー。
        string value = hex.TrimStart('#');
        if (value.Length != 6)
        {
            return new SolidColorBrush(Colors.Black);
        }

        byte r = Convert.ToByte(value[..2], 16);
        byte g = Convert.ToByte(value.Substring(2, 2), 16);
        byte b = Convert.ToByte(value.Substring(4, 2), 16);

        return new SolidColorBrush(Color.FromArgb(255, r, g, b));
    }
}
