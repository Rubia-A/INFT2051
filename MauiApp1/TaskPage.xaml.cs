using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace MauiApp1;

public partial class TaskPage : ContentPage
{
    // selected date from the main calendar
    private readonly string _selectedDateKey;

    // due date can be picked from the calendar
    private string _dueDateKey;

    // shown tasks for this day
    private readonly ObservableCollection<TaskItem> _tasks = new();

    // full saved json data
    private Dictionary<string, List<TaskItem>> _storageData = new();

    private string? _importedPdfPath;
    private string? _importedPdfName;

    // remember last import so user can undo
    private readonly List<ImportedTaskRecord> _lastImportedTasks = new();

    // custom dropdown selected values
    private string _selectedCategory = "To-do";
    private string _selectedPriority = "Medium";

    // current task being edited
    private string? _editingTaskId;

    private string StorageFilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "tasks.json");

    private string LastImportFilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "last_import.json");

    public TaskPage(string selectedDateKey)
    {
        InitializeComponent();

        NavigationPage.SetHasNavigationBar(this, false);

        _selectedDateKey = selectedDateKey;
        _dueDateKey = selectedDateKey;

        TaskEntry.TextChanged += (s, e) =>
        {
            StatusLabel.IsVisible = false;
        };

        TitleLabel.Text = "Daily Plan";

        if (DateTime.TryParseExact(
                selectedDateKey,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDate))
        {
            DateLabel.Text = string.Format(CultureInfo.InvariantCulture, "{0:dd MMMM yyyy}", parsedDate);
        }
        else
        {
            DateLabel.Text = selectedDateKey;
        }

        // set default custom dropdown values
        CategoryValueLabel.Text = _selectedCategory;
        PriorityValueLabel.Text = _selectedPriority;

        UpdateDateLabels();

        TaskCollectionView.ItemsSource = _tasks;

        SizeChanged += OnTaskPageSizeChanged;
        UpdateTaskResponsiveLayout();

        _ = LoadTasksAsync();
        _ = LoadLastImportAsync();
    }

    private async Task LoadTasksAsync()
    {
        try
        {
            _storageData = await LoadStorageDataAsync();
            RefreshCurrentDayTasks();
        }
        catch
        {
            StatusLabel.Text = "Failed to load tasks.";
            StatusLabel.IsVisible = true;
        }
    }

    private async Task<Dictionary<string, List<TaskItem>>> LoadStorageDataAsync()
    {
        if (!File.Exists(StorageFilePath))
            return new Dictionary<string, List<TaskItem>>();

        string json = await File.ReadAllTextAsync(StorageFilePath);
        var result = new Dictionary<string, List<TaskItem>>();

        using JsonDocument document = JsonDocument.Parse(json);

        foreach (JsonProperty dateGroup in document.RootElement.EnumerateObject())
        {
            var list = new List<TaskItem>();

            if (dateGroup.Value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (JsonElement itemElement in dateGroup.Value.EnumerateArray())
            {
                TaskItem? item = null;

                // support older plain text tasks too
                if (itemElement.ValueKind == JsonValueKind.String)
                {
                    item = TaskItem.FromOldText(itemElement.GetString() ?? string.Empty, dateGroup.Name);
                }
                else if (itemElement.ValueKind == JsonValueKind.Object)
                {
                    item = JsonSerializer.Deserialize<TaskItem>(itemElement.GetRawText());
                }

                if (item != null)
                {
                    item.Normalize(dateGroup.Name);
                    list.Add(item);
                }
            }

            result[dateGroup.Name] = list;
        }

        return result;
    }

    private async Task SaveTasksAsync()
    {
        try
        {
            _storageData[_selectedDateKey] = _tasks.ToList();
            await SaveStorageDataAsync();
        }
        catch
        {
            StatusLabel.Text = "Failed to save tasks.";
            StatusLabel.IsVisible = true;
        }
    }

    private async Task SaveStorageDataAsync()
    {
        string json = JsonSerializer.Serialize(_storageData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(StorageFilePath, json);
    }

    private async Task LoadLastImportAsync()
    {
        try
        {
            if (!File.Exists(LastImportFilePath))
                return;

            string json = await File.ReadAllTextAsync(LastImportFilePath);
            var records = JsonSerializer.Deserialize<List<ImportedTaskRecord>>(json);

            _lastImportedTasks.Clear();

            if (records != null)
            {
                _lastImportedTasks.AddRange(records);
            }
        }
        catch
        {
            _lastImportedTasks.Clear();
        }
    }

    private async Task SaveLastImportAsync()
    {
        string json = JsonSerializer.Serialize(_lastImportedTasks, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(LastImportFilePath, json);
    }

    private void ClearLastImportHistory()
    {
        _lastImportedTasks.Clear();

        if (File.Exists(LastImportFilePath))
        {
            File.Delete(LastImportFilePath);
        }
    }

    private void RefreshCurrentDayTasks()
    {
        _tasks.Clear();

        if (_storageData.TryGetValue(_selectedDateKey, out List<TaskItem>? savedTasks))
        {
            foreach (TaskItem task in savedTasks)
            {
                task.Normalize(_selectedDateKey);
                _tasks.Add(task);
            }
        }
    }

    private string FormatDateForDisplay(string dateKey)
    {
        if (DateTime.TryParseExact(
                dateKey,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDate))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:dd MMM yyyy}", parsedDate);
        }

        return dateKey;
    }

    private void UpdateDateLabels()
    {
        StartDateLabel.Text = $"Start Date: {FormatDateForDisplay(_selectedDateKey)}";
        DueDateLabel.Text = $"Due Date: {FormatDateForDisplay(_dueDateKey)}";
    }

    private TaskItem BuildCustomTaskItem(string rawTask)
    {
        bool isDeadline = _selectedCategory.Equals("Deadline", StringComparison.OrdinalIgnoreCase);

        return new TaskItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = rawTask,
            Category = _selectedCategory,
            Priority = _selectedPriority,
            StartDateKey = _selectedDateKey,
            DueDateKey = isDeadline ? _dueDateKey : string.Empty,
            IsCompleted = false
        };
    }

    // custom dropdown: category
    private void ToggleCategoryMenu(object? sender, TappedEventArgs e)
    {
        CategoryOptionsPanel.IsVisible = !CategoryOptionsPanel.IsVisible;
        PriorityOptionsPanel.IsVisible = false;
    }

    private void SelectCategoryClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string value)
        {
            _selectedCategory = value;
            CategoryValueLabel.Text = value;
            CategoryOptionsPanel.IsVisible = false;
        }
    }

    // custom dropdown: priority
    private void TogglePriorityMenu(object? sender, TappedEventArgs e)
    {
        PriorityOptionsPanel.IsVisible = !PriorityOptionsPanel.IsVisible;
        CategoryOptionsPanel.IsVisible = false;
    }

    private void SelectPriorityClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string value)
        {
            _selectedPriority = value;
            PriorityValueLabel.Text = value;
            PriorityOptionsPanel.IsVisible = false;
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
            return string.Format(CultureInfo.InvariantCulture, "{0:dd-MM-yy}", parsedDate);
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
                string dateKey = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}", parsedDate);

                if (!result.Contains(dateKey))
                {
                    result.Add(dateKey);
                }
            }
        }

        return result;
    }

    private Dictionary<string, List<TaskItem>> ExtractExamTasksFromPdf(string pdfText)
    {
        var examTasks = new Dictionary<string, List<TaskItem>>();

        var knownExams = new List<(string Module, DateTime Date, string Pattern)>
        {
            ("INFT2060", new DateTime(2026, 4, 20), @"Final Examination:\s*20\s*April\s*2026,\s*12\.30pm"),
            ("SENG2130", new DateTime(2026, 4, 23), @"Final Examination:\s*23\s*April\s*2026,\s*12\.30pm"),
            ("SENG2260", new DateTime(2026, 4, 27), @"Final Examination:\s*27\s*April\s*2026,\s*12\.30pm")
        };

        foreach (var exam in knownExams)
        {
            Match match = Regex.Match(pdfText, exam.Pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
                continue;

            string dateKey = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}", exam.Date);

            var item = new TaskItem
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"{exam.Module} Exam · 12.30pm",
                Category = "Deadline",
                Priority = "High",
                StartDateKey = dateKey,
                DueDateKey = dateKey,
                IsCompleted = false
            };

            if (!examTasks.ContainsKey(dateKey))
            {
                examTasks[dateKey] = new List<TaskItem>();
            }

            examTasks[dateKey].Add(item);
        }

        return examTasks;
    }

    private List<TaskItem> GenerateTasksForOneDate(string pdfText, string dateKey)
    {
        List<TaskItem> generatedTasks = new();

        string targetDate = ConvertDateKeyToPdfFormat(dateKey);
        int weekdayIndex = GetWeekdayIndex(dateKey);

        if (weekdayIndex < 0)
            return generatedTasks;

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

                generatedTasks.Add(new TaskItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"{sessionName}: {module} · {timing}",
                    Category = "Class",
                    Priority = "Medium",
                    StartDateKey = dateKey,
                    DueDateKey = string.Empty,
                    IsCompleted = false
                });
            }
        }

        AddSessionIfPossible(@"Session\s*1\s*Module", @"1\s*Timing", @"1\s*Class\s*type", "Session 1");
        AddSessionIfPossible(@"Session\s*2\s*Module", @"2\s*Timing", @"2\s*Class\s*type", "Session 2");
        AddSessionIfPossible(@"Session\s*3\s*Module", @"3\s*Timing", @"3\s*Class\s*type", "Session 3");

        return generatedTasks;
    }

    private async Task ImportTasksForAllDaysAsync(string pdfText)
    {
        _lastImportedTasks.Clear();

        var allDateKeys = ExtractAllDateKeysFromPdf(pdfText);
        var examTasks = ExtractExamTasksFromPdf(pdfText);

        int importedCount = 0;

        foreach (var examDay in examTasks)
        {
            importedCount += AddImportedItemsToDate(examDay.Key, examDay.Value);
        }

        foreach (string dateKey in allDateKeys)
        {
            var generatedTasks = GenerateTasksForOneDate(pdfText, dateKey);

            if (generatedTasks.Count == 0)
                continue;

            importedCount += AddImportedItemsToDate(dateKey, generatedTasks);
        }

        RefreshCurrentDayTasks();
        await SaveStorageDataAsync();
        await SaveLastImportAsync();

        PdfStatusLabel.Text = importedCount == 0
            ? "No new items were imported. They may already exist."
            : $"{importedCount} item(s) imported for all days.";
    }

    private int AddImportedItemsToDate(string dateKey, List<TaskItem> items)
    {
        int importedCount = 0;

        if (!_storageData.ContainsKey(dateKey))
        {
            _storageData[dateKey] = new List<TaskItem>();
        }

        foreach (TaskItem item in items)
        {
            bool alreadyExists = _storageData[dateKey].Any(t =>
                t.Title == item.Title &&
                t.Category == item.Category &&
                t.StartDateKey == item.StartDateKey &&
                t.DueDateKey == item.DueDateKey);

            if (!alreadyExists)
            {
                item.Normalize(dateKey);

                _storageData[dateKey].Add(item);

                _lastImportedTasks.Add(new ImportedTaskRecord
                {
                    DateKey = dateKey,
                    TaskId = item.Id
                });

                importedCount++;
            }
        }

        return importedCount;
    }

    private async Task ImportTasksForCurrentDayAsync(string pdfText)
    {
        _lastImportedTasks.Clear();

        var generatedTasks = GenerateTasksForOneDate(pdfText, _selectedDateKey);
        var examTasks = ExtractExamTasksFromPdf(pdfText);

        if (examTasks.TryGetValue(_selectedDateKey, out List<TaskItem>? currentDayExamTasks))
        {
            generatedTasks.AddRange(currentDayExamTasks);
        }

        if (generatedTasks.Count == 0)
        {
            await SaveLastImportAsync();
            PdfStatusLabel.Text = "No class or exam was found for this selected date. Try another date or use 'Import PDF for All Days'.";
            return;
        }

        int importedCount = 0;

        foreach (TaskItem item in generatedTasks)
        {
            bool alreadyExists = _tasks.Any(t =>
                t.Title == item.Title &&
                t.Category == item.Category &&
                t.StartDateKey == item.StartDateKey &&
                t.DueDateKey == item.DueDateKey);

            if (!alreadyExists)
            {
                item.Normalize(_selectedDateKey);
                _tasks.Add(item);

                _lastImportedTasks.Add(new ImportedTaskRecord
                {
                    DateKey = _selectedDateKey,
                    TaskId = item.Id
                });

                importedCount++;
            }
        }

        await SaveTasksAsync();
        await SaveLastImportAsync();

        PdfStatusLabel.Text = importedCount == 0
            ? "No new items were imported. They may already exist."
            : $"{importedCount} item(s) imported for this day.";
    }

    private async Task UndoLastImportAsync()
    {
        if (_lastImportedTasks.Count == 0)
        {
            PdfStatusLabel.Text = "There is no import to undo.";
            return;
        }

        int undoneCount = 0;

        foreach (var item in _lastImportedTasks)
        {
            if (_storageData.TryGetValue(item.DateKey, out var taskList))
            {
                TaskItem? target = taskList.FirstOrDefault(t => t.Id == item.TaskId);

                if (target != null)
                {
                    taskList.Remove(target);
                    undoneCount++;
                }

                if (taskList.Count == 0)
                {
                    _storageData.Remove(item.DateKey);
                }
            }
        }

        RefreshCurrentDayTasks();
        await SaveStorageDataAsync();

        ClearLastImportHistory();

        PdfStatusLabel.Text = $"{undoneCount} imported item(s) removed.";
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

            PdfStatusLabel.Text = "Text extracted. Importing all items...";

            await ImportTasksForAllDaysAsync(pdfText);
        }
        catch (Exception ex)
        {
            PdfStatusLabel.Text = $"Failed to import all items: {ex.Message}";
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

            PdfStatusLabel.Text = "Text extracted. Generating items...";

            await ImportTasksForCurrentDayAsync(pdfText);
        }
        catch (Exception ex)
        {
            PdfStatusLabel.Text = $"Failed to import PDF: {ex.Message}";
        }
    }

    private async void UndoLastImportClicked(object sender, EventArgs e)
    {
        await UndoLastImportAsync();
    }

    private async void AddTaskClicked(object sender, EventArgs e)
    {
        string? rawTask = TaskEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(rawTask))
        {
            StatusLabel.Text = "Please enter an item first.";
            StatusLabel.IsVisible = true;
            return;
        }

        TaskItem newItem = BuildCustomTaskItem(rawTask);

        _tasks.Add(newItem);
        TaskEntry.Text = string.Empty;

        await SaveTasksAsync();
    }

    private async void ToggleCompleteClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string taskId)
        {
            TaskItem? item = _tasks.FirstOrDefault(t => t.Id == taskId);

            if (item != null)
            {
                item.IsCompleted = !item.IsCompleted;
                item.RefreshVisualState();

                await SaveTasksAsync();
            }
        }
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

    // open custom edit popup
    private void OnEditTaskTapped(object sender, TappedEventArgs e)
    {
        if (sender is Label label && label.BindingContext is TaskItem task)
        {
            _editingTaskId = task.Id;
            EditEntry.Text = task.Title;
            EditOverlay.IsVisible = true;
        }
    }

    private void CancelEditClicked(object sender, EventArgs e)
    {
        _editingTaskId = null;
        EditEntry.Text = string.Empty;
        EditOverlay.IsVisible = false;
    }

    private async void SaveEditClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_editingTaskId))
            return;

        string newTitle = EditEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(newTitle))
            return;

        TaskItem? task = _tasks.FirstOrDefault(t => t.Id == _editingTaskId);

        if (task != null)
        {
            task.Title = newTitle;
            task.RefreshVisualState();
            await SaveTasksAsync();
        }

        _editingTaskId = null;
        EditEntry.Text = string.Empty;
        EditOverlay.IsVisible = false;
    }

    private async void ChooseDueDateClicked(object sender, EventArgs e)
    {
        MainPage.IsChoosingDueDate = true;
        MainPage.DueDateTargetPage = this;

        await Navigation.PopAsync();
    }

    public void SetDueDateFromCalendar(string dateKey)
    {
        _dueDateKey = dateKey;
        UpdateDateLabels();
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
            TaskRootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            TaskRootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430) });
            TaskRootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            Grid.SetRow(TopPanel, 0);
            Grid.SetColumn(TopPanel, 0);

            Grid.SetRow(ListPanel, 0);
            Grid.SetColumn(ListPanel, 1);

            TaskRootGrid.Padding = new Thickness(22, 18);
            TopPanel.Spacing = 14;
            ListPanel.Spacing = 12;

            TaskEntry.HeightRequest = 48;
            AddTaskButton.HeightRequest = 48;
            PdfButton.HeightRequest = 48;

            TaskCollectionView.HeightRequest = Height * 0.78;
        }
        else
        {
            TaskRootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TaskRootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TaskRootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            Grid.SetRow(TopPanel, 0);
            Grid.SetColumn(TopPanel, 0);

            Grid.SetRow(ListPanel, 1);
            Grid.SetColumn(ListPanel, 0);

            TaskRootGrid.Padding = new Thickness(20, 20);
            TopPanel.Spacing = 14;
            ListPanel.Spacing = 12;

            TaskEntry.HeightRequest = 44;
            AddTaskButton.HeightRequest = 44;
            PdfButton.HeightRequest = 44;

            TaskCollectionView.HeightRequest = Height * 0.48;
        }
    }
}

public class ImportedTaskRecord
{
    public string DateKey { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
}

public class TaskItem : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private bool _isCompleted;

    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = "To-do";
    public string Priority { get; set; } = "Medium";
    public string StartDateKey { get; set; } = string.Empty;
    public string DueDateKey { get; set; } = string.Empty;

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted != value)
            {
                _isCompleted = value;
                OnPropertyChanged();
                RefreshVisualState();
            }
        }
    }

    public bool HasDueDate => !string.IsNullOrWhiteSpace(DueDateKey);

    public string StartDisplayText => $"Start: {FormatDate(StartDateKey)}";
    public string DueDisplayText => $"Due: {FormatDate(DueDateKey)}";

    public string StatusText => IsCompleted ? "Completed" : "";
    public string CompletionButtonText => IsCompleted ? "Undo" : "✓ Done";
    public string CompletionButtonTextColor => IsCompleted ? "#6C4D69" : "#2E6B4E";

    public TextDecorations TitleDecoration => IsCompleted
        ? TextDecorations.Strikethrough
        : TextDecorations.None;

    // card colors
    public Color CardBackgroundColor => IsCompleted
        ? Color.FromArgb("#FCF7FC")
        : Color.FromArgb("#FFFFFF");

    public Color CardStrokeColor => IsCompleted
        ? Color.FromArgb("#E6D8E9")
        : Color.FromArgb("#E8D1EA");

    public Color TitleTextColor => IsCompleted
        ? Color.FromArgb("#B18DAF")
        : Color.FromArgb("#744A6D");

    public Color DetailTextColor => IsCompleted
        ? Color.FromArgb("#B18DAF")
        : Color.FromArgb("#956F92");

    public Color BadgeBackgroundColor
    {
        get
        {
            if (Category.Equals("Deadline", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb("#FFD8E8");

            if (Category.Equals("Class", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb("#DCEAFF");

            return Color.FromArgb("#F3D5FF");
        }
    }

    public Color BadgeTextColor => Color.FromArgb("#6C4D69");

    public Color PriorityBackgroundColor
    {
        get
        {
            if (Priority.Equals("High", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb("#FFE3DF");

            if (Priority.Equals("Low", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb("#DFF7EA");

            return Color.FromArgb("#FFF2C9");
        }
    }

    public Color PriorityTextColor => Color.FromArgb("#6C4D69");
    public Color StatusTextColor => Color.FromArgb("#6DA07D");

    public Color CompletionButtonColor => IsCompleted
        ? Color.FromArgb("#F5E9F6")
        : Color.FromArgb("#DFF6E7");

    public static TaskItem FromOldText(string oldText, string fallbackDateKey)
    {
        string category = oldText.Contains("DDL", StringComparison.OrdinalIgnoreCase)
            ? "Deadline"
            : "To-do";

        string priority = oldText.Contains("High", StringComparison.OrdinalIgnoreCase)
            ? "High"
            : oldText.Contains("Low", StringComparison.OrdinalIgnoreCase)
                ? "Low"
                : "Medium";

        string title = oldText;
        string startDateKey = fallbackDateKey;
        string dueDateKey = string.Empty;

        Match titleMatch = Regex.Match(
            oldText,
            @"^\s*\[(?<cat>[^\]]+)\]\s*\[(?<priority>[^\]]+)\]\s*(?<title>.*?)(?:\s*\|\s*Start:|\s*-\s*Due:|$)",
            RegexOptions.IgnoreCase);

        if (titleMatch.Success)
        {
            string rawCategory = titleMatch.Groups["cat"].Value.Trim();
            string rawPriority = titleMatch.Groups["priority"].Value.Trim();

            title = titleMatch.Groups["title"].Value.Trim();

            if (rawCategory.Equals("DDL", StringComparison.OrdinalIgnoreCase))
                category = "Deadline";
            else if (rawCategory.Equals("Task", StringComparison.OrdinalIgnoreCase))
                category = "To-do";

            priority = NormalizePriority(rawPriority);
        }

        Match startMatch = Regex.Match(oldText, @"Start:\s*(?<date>\d{1,2}\s+[A-Za-z]{3}\s+\d{4})", RegexOptions.IgnoreCase);
        Match dueMatch = Regex.Match(oldText, @"Due:\s*(?<date>\d{1,2}\s+[A-Za-z]{3}\s+\d{4})", RegexOptions.IgnoreCase);

        if (startMatch.Success)
        {
            startDateKey = ConvertDisplayDateToKey(startMatch.Groups["date"].Value, fallbackDateKey);
        }

        if (dueMatch.Success)
        {
            dueDateKey = ConvertDisplayDateToKey(dueMatch.Groups["date"].Value, fallbackDateKey);
        }
        else if (category == "Deadline")
        {
            dueDateKey = fallbackDateKey;
        }

        return new TaskItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = string.IsNullOrWhiteSpace(title) ? "Untitled item" : title,
            Category = category,
            Priority = priority,
            StartDateKey = startDateKey,
            DueDateKey = dueDateKey,
            IsCompleted = false
        };
    }

    public void Normalize(string fallbackDateKey)
    {
        if (string.IsNullOrWhiteSpace(Id))
            Id = Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(Category))
            Category = "To-do";

        if (string.IsNullOrWhiteSpace(Priority))
            Priority = "Medium";

        Priority = NormalizePriority(Priority);

        if (string.IsNullOrWhiteSpace(StartDateKey))
            StartDateKey = fallbackDateKey;

        if (string.IsNullOrWhiteSpace(Title))
            Title = "Untitled item";

        RefreshVisualState();
    }

    public void RefreshVisualState()
    {
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(HasDueDate));
        OnPropertyChanged(nameof(StartDisplayText));
        OnPropertyChanged(nameof(DueDisplayText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CompletionButtonText));
        OnPropertyChanged(nameof(CompletionButtonTextColor));
        OnPropertyChanged(nameof(TitleDecoration));
        OnPropertyChanged(nameof(CardBackgroundColor));
        OnPropertyChanged(nameof(CardStrokeColor));
        OnPropertyChanged(nameof(TitleTextColor));
        OnPropertyChanged(nameof(DetailTextColor));
        OnPropertyChanged(nameof(BadgeBackgroundColor));
        OnPropertyChanged(nameof(BadgeTextColor));
        OnPropertyChanged(nameof(PriorityBackgroundColor));
        OnPropertyChanged(nameof(PriorityTextColor));
        OnPropertyChanged(nameof(StatusTextColor));
        OnPropertyChanged(nameof(CompletionButtonColor));
    }

    private static string NormalizePriority(string value)
    {
        if (value.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            return "Medium";

        if (value.Equals("High", StringComparison.OrdinalIgnoreCase))
            return "High";

        if (value.Equals("Low", StringComparison.OrdinalIgnoreCase))
            return "Low";

        return "Medium";
    }

    private static string ConvertDisplayDateToKey(string displayDate, string fallbackDateKey)
    {
        string[] formats = { "d MMM yyyy", "dd MMM yyyy" };

        if (DateTime.TryParseExact(
                displayDate,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDate))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}", parsedDate);
        }

        return fallbackDateKey;
    }

    private static string FormatDate(string dateKey)
    {
        if (DateTime.TryParseExact(
                dateKey,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDate))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:dd MMM yyyy}", parsedDate);
        }

        return dateKey;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}