using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace MauiApp1;

public partial class TaskPage : ContentPage
{
    private readonly string _selectedDateKey;
    private readonly ObservableCollection<TaskItem> _tasks = new();

    private Dictionary<string, List<string>> _storageData = new();

    private string StorageFilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "tasks.json");

    public TaskPage(string selectedDateKey)
    {
        InitializeComponent();

        _selectedDateKey = selectedDateKey;

        TaskEntry.TextChanged += (s, e) =>
        {
            StatusLabel.IsVisible = false;
        };


        TitleLabel.Text = "Tasks";

        if (DateTime.TryParseExact(
                selectedDateKey,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDate))
        {
            DateLabel.Text = parsedDate.ToString("dd MMMM yyyy");
        }
        else
        {
            DateLabel.Text = selectedDateKey;
        }

        TaskCollectionView.ItemsSource = _tasks; 

        _ = LoadTasksAsync();
    }

    private async Task LoadTasksAsync()
    {
        try
        {
            if (File.Exists(StorageFilePath))
            {
                string json = await File.ReadAllTextAsync(StorageFilePath);

                _storageData = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                               ?? new Dictionary<string, List<string>>();
            }
            else
            {
                _storageData = new Dictionary<string, List<string>>();
            }

            _tasks.Clear();

            if (_storageData.TryGetValue(_selectedDateKey, out List<string>? savedTasks))
            {
                foreach (string taskText in savedTasks)
                {
                    _tasks.Add(new TaskItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Text = taskText
                    });
                }
            }
        }
        catch
        {
            StatusLabel.Text = "Failed to load tasks.";
            StatusLabel.IsVisible = true;
        }
    }

    private async Task SaveTasksAsync()
    {
        try
        {
            _storageData[_selectedDateKey] = _tasks.Select(t => t.Text).ToList();

            string json = JsonSerializer.Serialize(_storageData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(StorageFilePath, json);
        }
        catch
        {
            StatusLabel.Text = "Failed to save tasks.";
            StatusLabel.IsVisible = true; 
        }
    }

    private async void AddTaskClicked(object sender, EventArgs e)
    {
        string? newTask = TaskEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(newTask))
        {
            StatusLabel.Text = "Please enter a task first.";
            StatusLabel.IsVisible = true;
            return;
        }

        _tasks.Add(new TaskItem
        {
            Id = Guid.NewGuid().ToString(),
            Text = newTask
        });

        TaskEntry.Text = string.Empty;

        await SaveTasksAsync();
    }

    private async void DeleteTaskClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string taskId)
        {
            TaskItem? item = _tasks.FirstOrDefault(t => t.Id == taskId);

            if (item != null)
            {
                _tasks.Remove(item);
                await SaveTasksAsync();
            }
        }
    }

    private async void OnEditTaskTapped(object sender, TappedEventArgs e)
    {
        if (sender is Label label && label.BindingContext is TaskItem task)
        {
            string? result = await DisplayPromptAsync(
                "Edit Task",
                "Update your task:",
                initialValue: task.Text
            );

            if (!string.IsNullOrWhiteSpace(result))
            {
                task.Text = result.Trim();

                TaskCollectionView.ItemsSource = null;
                TaskCollectionView.ItemsSource = _tasks;

                await SaveTasksAsync();
            }
        }
    }
    private async void PdfComingSoonClicked(object sender, EventArgs e)
    {
         StatusLabel.Text =
            "Coming Soon,This space is reserved for future PDF import using File Picker / File System and OCR.,OK";
        StatusLabel.IsVisible = true;
    }
}

public class TaskItem
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}