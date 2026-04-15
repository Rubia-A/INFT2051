using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MauiApp1;

public partial class TaskPage : ContentPage
{
    private readonly string _selectedDateKey;
    private readonly ObservableCollection<TaskItem> _tasks = new();

    private Dictionary<string, List<string>> _storageData = new();

    private string? _importedPdfPath;
    private string? _importedPdfName;

    private readonly List<(string DateKey, string TaskText)> _lastImportedTasks = new();
    private string StorageFilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "tasks.json");

    public TaskPage(string selectedDateKey)
    {
        InitializeComponent();

        NavigationPage.SetHasNavigationBar(this, false);

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

        SizeChanged += OnTaskPageSizeChanged;
        UpdateTaskResponsiveLayout();

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
    private string ConvertDateKeyToPdfFormat(string dateKey)
    {
        if (DateTime.TryParseExact(
            dateKey,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime parsedDate))
        {
            return parsedDate.ToString("dd-MM-yy");
        }

        return dateKey;
    }
    private int GetWeekdayIndex(string dateKey)
    {
        if (DateTime.TryParseExact(
            dateKey,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime parsedDate))
        {
            int dayOfWeek = (int)parsedDate.DayOfWeek;
            return dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        }

        return -1;
    }
    private string ExtractSectionText(string source, string startPattern, string endPattern)
    {
        var match = Regex.Match(
            source,
            startPattern + @"(.*?)" + endPattern,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success ? match.Groups[1].Value : string.Empty;
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

    private string ExtractPdfText(string path)
    {
        var textList = new List<string>();

        using (var doc = PdfDocument.Open(path))
        {
            foreach (var page in doc.GetPages())
            {
                textList.Add(page.Text);
            }
        }

        return string.Join("\n", textList);
    }
    private List<string> ExtractAllDateKeysFromPdf(string pdfText)
    {
        var result = new List<string>();

        string normalizedText = Regex.Replace(pdfText, @"\s+", " ").Trim();

        var matches = Regex.Matches(normalizedText, @"\d{2}-\d{2}-\d{2}");

        foreach (Match match in matches)
        {
            string pdfDate = match.Value;

            if (DateTime.TryParseExact(
                pdfDate,
                "dd-MM-yy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDate))
            {
                string dateKey = parsedDate.ToString("yyyy-MM-dd");

                if (!result.Contains(dateKey))
                {
                    result.Add(dateKey);
                }
            }
        }

        return result;
    }

    private async Task ImportTasksForAllDaysAsync(string pdfText)
    {
        _lastImportedTasks.Clear();

        var allDateKeys = ExtractAllDateKeysFromPdf(pdfText);
        int importedCount = 0;

        foreach (string dateKey in allDateKeys)
        {
            var generatedTasks = GenerateTasksForOneDate(pdfText, dateKey);

            if (generatedTasks.Count == 0)
                continue;

            if (!_storageData.ContainsKey(dateKey))
            {
                _storageData[dateKey] = new List<string>();
            }

            foreach (string taskText in generatedTasks)
            {
                bool alreadyExists = _storageData[dateKey].Contains(taskText);

                if (!alreadyExists)
                {
                    _storageData[dateKey].Add(taskText);
                    _lastImportedTasks.Add((dateKey, taskText));
                    importedCount++;
                }
            }
        }

        _tasks.Clear();
        if (_storageData.TryGetValue(_selectedDateKey, out var tasksForCurrentDay))
        {
            foreach (var taskText in tasksForCurrentDay)
            {
                _tasks.Add(new TaskItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Text = taskText
                });
            }
        }

        string json = JsonSerializer.Serialize(_storageData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(StorageFilePath, json);

        PdfStatusLabel.Text = $"{importedCount} task(s) imported for all days.";
    }

    private List<string> GenerateTasksForOneDate(string pdfText, string dateKey)
    {
        List<string> generatedTasks = new();

        string targetDate = ConvertDateKeyToPdfFormat(dateKey);
        int weekdayIndex = GetWeekdayIndex(dateKey);

        if (weekdayIndex < 0)
            return generatedTasks;

        // avide issues caused by newlines and multiple spaces in the PDF text
        string normalizedText = Regex.Replace(pdfText, @"\s+", " ").Trim();

        int targetDateIndex = normalizedText.IndexOf(targetDate, StringComparison.OrdinalIgnoreCase);
        if (targetDateIndex == -1)
            return generatedTasks;

        string weekHeaderPattern = @"MONDAY\s*TUESDAY\s*WEDNESDAY\s*THURSDAY\s*FRIDAY\s*SATURDAY\s*SUNDAY";

        var weekHeaderMatches = Regex.Matches(normalizedText, weekHeaderPattern, RegexOptions.IgnoreCase);

        int weekStartIndex = -1;
        int nextWeekIndex = normalizedText.Length;

        foreach (Match match in weekHeaderMatches)
        {
            if (match.Index <= targetDateIndex)
            {
                weekStartIndex = match.Index;
            }
            else
            {
                nextWeekIndex = match.Index;
                break;
            }
        }

        if (weekStartIndex == -1)
            return generatedTasks;

        string weekBlock = normalizedText.Substring(weekStartIndex, nextWeekIndex - weekStartIndex);

        void AddSessionIfPossible(string moduleStart, string timingStart, string classTypeStart, string sessionName)
        {
            string moduleSection = ExtractSectionText(weekBlock, moduleStart, timingStart);
            string timingSection = ExtractSectionText(weekBlock, timingStart, classTypeStart);

            if (string.IsNullOrWhiteSpace(moduleSection) || string.IsNullOrWhiteSpace(timingSection))
                return;

            var modules = Regex.Matches(
                    moduleSection,
                    @"UON\s*_?\s*Tri126\s*_?\s*FT\s*_?\s*[A-Z]{4}\d{4}\s*_?\s*S\d+\s*_(?:LEC|LAB_[AB])",
                    RegexOptions.IgnoreCase)
                .Select(m => Regex.Replace(m.Value, @"\s+", " ").Trim())
                .ToList();

            //extract time
            var timings = Regex.Matches(
                    timingSection,
                    @"\d{1,2}:\d{2}\s*[AP]M\s*-\s*\d{1,2}:\d{2}\s*[AP]M",
                    RegexOptions.IgnoreCase)
                .Select(m => Regex.Replace(m.Value, @"\s+", " ").Trim())
                .ToList();

            if (weekdayIndex < modules.Count && weekdayIndex < timings.Count)
            {
                string module = modules[weekdayIndex];
                string timing = timings[weekdayIndex];

                module = Regex.Replace(module, @"UON\s*_?\s*Tri126\s*_?\s*FT\s*_?\s*", "", RegexOptions.IgnoreCase);
                module = module.Replace("_", " ").Trim();

                generatedTasks.Add($"{sessionName}: {module} | {timing}");
            }
        }

        AddSessionIfPossible(
            @"Session\s*1\s*Module",
            @"1\s*Timing",
            @"1\s*Class\s*type",
            "Session 1");

        AddSessionIfPossible(
            @"Session\s*2\s*Module",
            @"2\s*Timing",
            @"2\s*Class\s*type",
            "Session 2");

        AddSessionIfPossible(
            @"Session\s*3\s*Module",
            @"3\s*Timing",
            @"3\s*Class\s*type",
            "Session 3");

        return generatedTasks;
    }
    private async Task UndoLastImportAsync()
    {
        if (_lastImportedTasks.Count == 0)
        {
            PdfStatusLabel.Text = "There is no import to undo.";
            return;
        }

        foreach (var item in _lastImportedTasks)
        {
            if (_storageData.TryGetValue(item.DateKey, out var taskList))
            {
                taskList.Remove(item.TaskText);
            }
        }

        // reflect changes in the UI for the current day
        _tasks.Clear();
        if (_storageData.TryGetValue(_selectedDateKey, out var currentTasks))
        {
            foreach (var taskText in currentTasks)
            {
                _tasks.Add(new TaskItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Text = taskText
                });
            }
        }

        string json = JsonSerializer.Serialize(_storageData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(StorageFilePath, json);

        int undoneCount = _lastImportedTasks.Count;
        _lastImportedTasks.Clear();

        PdfStatusLabel.Text = $"{undoneCount} imported task(s) removed.";
    }
    private async Task ImportTasksForCurrentDayAsync(string pdfText)
    {
        _lastImportedTasks.Clear();
        var generatedTasks = GenerateTasksForOneDate(pdfText, _selectedDateKey);

        if (generatedTasks.Count == 0)
        {
            PdfStatusLabel.Text = "No tasks found for this date.";
            return;
        }

        int importedCount = 0;

        foreach (var taskText in generatedTasks)
        {
            bool alreadyExists = _tasks.Any(t => t.Text == taskText);
            if (!alreadyExists)
            {
                _tasks.Add(new TaskItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Text = taskText
                });

                _lastImportedTasks.Add((_selectedDateKey, taskText));
                importedCount++;
            }
        }

        await SaveTasksAsync();
        PdfStatusLabel.Text = $"{importedCount} task(s) imported for this day.";
    }

    private async void ImportAllPdfClicked(object sender, EventArgs e)
    {
        try
        {
            var pdfFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, new[] { ".pdf" } },
            { DevicePlatform.Android, new[] { "application/pdf" } },
            { DevicePlatform.iOS, new[] { "com.adobe.pdf" } },
            { DevicePlatform.MacCatalyst, new[] { "com.adobe.pdf" } }
        });

            PickOptions options = new()
            {
                PickerTitle = "Please select a PDF file",
                FileTypes = pdfFileType
            };

            var result = await FilePicker.Default.PickAsync(options);

            if (result == null)
            {
                PdfStatusLabel.Text = "Import cancelled.";
                return;
            }

            string safeFileName = $"{Guid.NewGuid()}_{result.FileName}";
            string localPath = Path.Combine(FileSystem.AppDataDirectory, safeFileName);

            using (Stream sourceStream = await result.OpenReadAsync())
            using (FileStream localFileStream = File.Create(localPath))
            {
                await sourceStream.CopyToAsync(localFileStream);
                await localFileStream.FlushAsync();
            }

            _importedPdfPath = localPath;
            _importedPdfName = result.FileName;

            PdfFileNameLabel.Text = $"Imported file: {_importedPdfName}";
            PdfStatusLabel.Text = "Reading PDF text...";

            string pdfText = await Task.Run(() => ExtractPdfText(_importedPdfPath));

            PdfStatusLabel.Text = "Text extracted. Importing all tasks...";

            await ImportTasksForAllDaysAsync(pdfText);
        }
        catch (Exception ex)
        {
            PdfStatusLabel.Text = $"Failed to import all tasks: {ex.Message}";
        }
    }

    private async void UndoLastImportClicked(object sender, EventArgs e)
    {
        await UndoLastImportAsync();
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

    private async void ImportPdfClicked(object sender, EventArgs e)
    {
        try
        {
            var pdfFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, new[] { ".pdf" } },
            { DevicePlatform.Android, new[] { "application/pdf" } },
            { DevicePlatform.iOS, new[] { "com.adobe.pdf" } },
            { DevicePlatform.MacCatalyst, new[] { "com.adobe.pdf" } }
        });

            PickOptions options = new()
            {
                PickerTitle = "Please select a PDF file",
                FileTypes = pdfFileType
            };

            var result = await FilePicker.Default.PickAsync(options);

            if (result == null)
            {
                PdfStatusLabel.Text = "Import cancelled.";
                return;
            }

            string safeFileName = $"{Guid.NewGuid()}_{result.FileName}";
            string localPath = Path.Combine(FileSystem.AppDataDirectory, safeFileName);

            using (Stream sourceStream = await result.OpenReadAsync())
            using (FileStream localFileStream = File.Create(localPath))
            {
                await sourceStream.CopyToAsync(localFileStream);
                await localFileStream.FlushAsync();
            }

            _importedPdfPath = localPath;
            _importedPdfName = result.FileName;

            PdfFileNameLabel.Text = $"Imported file: {_importedPdfName}";
            PdfStatusLabel.Text = "Reading PDF text...";

            string pdfText = await Task.Run(() => ExtractPdfText(_importedPdfPath));

            PdfStatusLabel.Text = "Text extracted. Generating tasks...";

            await ImportTasksForCurrentDayAsync(pdfText);
        }
        catch (Exception ex)
        {
            PdfStatusLabel.Text = $"Failed to import PDF: {ex.Message}";
        }
    }

    private async void BackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void OnTaskPageSizeChanged(object? sender, EventArgs e)
    {
        UpdateTaskResponsiveLayout();
    }

    private void UpdateTaskResponsiveLayout()
    {
        if (Width <= 0 || Height <= 0)
            return;

        bool isLandscape = Width > Height;

        TaskRootGrid.RowDefinitions.Clear();
        TaskRootGrid.ColumnDefinitions.Clear();

        if (isLandscape)
        {
            // left and right layout
            TaskRootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            TaskRootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            TaskRootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            Grid.SetRow(TopPanel, 0);
            Grid.SetColumn(TopPanel, 0);

            Grid.SetRow(ListPanel, 0);
            Grid.SetColumn(ListPanel, 1);

            TaskRootGrid.Padding = new Thickness(20, 18);
            TopPanel.Spacing = 14;
            ListPanel.Spacing = 10;

            TaskEntry.HeightRequest = 48;
            AddTaskButton.HeightRequest = 48;
            PdfButton.HeightRequest = 48;

            TaskCollectionView.HeightRequest = Height * 0.72;
        }
        else
        {
            // up and down layout
            TaskRootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TaskRootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TaskRootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            Grid.SetRow(TopPanel, 0);
            Grid.SetColumn(TopPanel, 0);

            Grid.SetRow(ListPanel, 1);
            Grid.SetColumn(ListPanel, 0);

            TaskRootGrid.Padding = new Thickness(18, 18);
            TopPanel.Spacing = 12;
            ListPanel.Spacing = 10;

            TaskEntry.HeightRequest = 42;
            AddTaskButton.HeightRequest = 42;
            PdfButton.HeightRequest = 42;

            TaskCollectionView.HeightRequest = Height * 0.42;
        }
    }
}

public class TaskItem
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}