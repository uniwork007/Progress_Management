using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Progress_Management.Models;
using Progress_Management.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Progress_Management;

public sealed partial class WorkerMaintenanceDialog : ContentDialog
{
    private List<WorkerEditRecord> _workers = [];
    private WorkerEditRecord? _selectedWorker;
    private bool _isBinding;

    public bool WasChanged { get; private set; }

    private ListView WorkerList => (ListView)FindName("WorkerListView");

    private TextBlock WorkerCountText => (TextBlock)FindName("WorkerCountTextBlock");

    private TextBox WorkerIdInput => (TextBox)FindName("IdTextBox");

    private TextBox WorkerNameInput => (TextBox)FindName("NameTextBox");

    private NumberBox WorkerSortOrderInput => (NumberBox)FindName("SortOrderNumberBox");

    private TextBlock MessageText => (TextBlock)FindName("MessageTextBlock");

    public WorkerMaintenanceDialog()
    {
        InitializeComponent();
        LoadWorkers();
    }

    private void LoadWorkers(string? selectedWorkerId = null)
    {
        _isBinding = true;
        _workers = ScenarioRepository.LoadWorkersForEdit();
        WorkerList.ItemsSource = _workers;
        WorkerList.DisplayMemberPath = nameof(WorkerEditRecord.Name);
        WorkerCountText.Text = $"{_workers.Count}件";

        var selected = string.IsNullOrWhiteSpace(selectedWorkerId)
            ? _workers.FirstOrDefault()
            : _workers.FirstOrDefault(worker => worker.Id == selectedWorkerId) ?? _workers.FirstOrDefault();
        WorkerList.SelectedItem = selected;
        _isBinding = false;

        LoadWorker(selected);
    }

    private void WorkerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBinding)
        {
            return;
        }

        LoadWorker(WorkerList.SelectedItem as WorkerEditRecord);
    }

    private void LoadWorker(WorkerEditRecord? worker)
    {
        _selectedWorker = worker;
        WorkerIdInput.Text = worker?.Id ?? "";
        WorkerNameInput.Text = worker?.Name ?? "";
        WorkerSortOrderInput.Value = worker is null || worker.SortOrder <= 0 ? double.NaN : worker.SortOrder;
        WorkerIdInput.IsEnabled = worker is null;
        MessageText.Text = "";
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        WorkerList.SelectedItem = null;
        LoadWorker(null);
        WorkerIdInput.Focus(FocusState.Programmatic);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        MessageText.Text = "";

        var id = WorkerIdInput.Text.Trim();
        var name = WorkerNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            MessageText.Text = "担当者IDと名前は必須です。";
            return;
        }

        var record = new WorkerEditRecord
        {
            Id = _selectedWorker?.Id ?? id,
            Name = name,
            SortOrder = double.IsNaN(WorkerSortOrderInput.Value)
                ? 0
                : Convert.ToInt32(WorkerSortOrderInput.Value)
        };

        ScenarioRepository.SaveWorker(record);
        WasChanged = true;
        LoadWorkers(record.Id);
        MessageText.Text = "保存しました。";
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        MessageText.Text = "";
        if (_selectedWorker is null)
        {
            MessageText.Text = "削除する担当者を選択してください。";
            return;
        }

        if (ScenarioRepository.DeleteWorker(_selectedWorker.Id, out var message))
        {
            WasChanged = true;
            LoadWorkers();
            MessageText.Text = message;
            return;
        }

        MessageText.Text = message;
    }
}
