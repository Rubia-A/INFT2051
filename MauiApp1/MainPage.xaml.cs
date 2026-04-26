using System.Globalization;
using System.Text.Json;

namespace MauiApp1;

public partial class MainPage : ContentPage
{
    // remember the month the calendar is showing
    private int currentYear;
    private int currentMonth;

    // used when TaskPage asks MainPage to choose a due date
    public static bool IsChoosingDueDate { get; set; } = false;
    public static TaskPage? DueDateTargetPage { get; set; }

    // Save temporary data when user goes back to calendar to choose a due date
    public static string? PendingStartDateKey { get; set; }
    public static string? PendingDraftTitle { get; set; }
    public static string? PendingDraftCategory { get; set; }
    public static string? PendingDraftPriority { get; set; }

    // local storage file
    private string StorageFilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "tasks.json");

    public MainPage()
    {
        InitializeComponent();

        NavigationPage.SetHasNavigationBar(this, false);

        currentYear = DateTime.Now.Year;
        currentMonth = DateTime.Now.Month;

        SizeChanged += OnPageSizeChanged;

        GenerateCalendar();
        UpdateResponsiveLayout();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // refresh calendar after returning from TaskPage
        GenerateCalendar();
    }

    private void GenerateCalendar()
    {
        CalendarGrid.Children.Clear();

        int year = currentYear;
        int month = currentMonth;

        int daysInMonth = DateTime.DaysInMonth(year, month);
        DateTime todayDate = DateTime.Now;

        DateTime firstDay = new DateTime(year, month, 1);
        int startDay = (int)firstDay.DayOfWeek;

        // convert from Sunday-first to Monday-first
        startDay = (startDay == 0) ? 6 : startDay - 1;

        MonthLabel.Text = IsChoosingDueDate
            ? $"Choose Due Date · {string.Format(CultureInfo.InvariantCulture, "{0:MMMM yyyy}", new DateTime(year, month, 1))}"
            : string.Format(CultureInfo.InvariantCulture, "{0:MMMM yyyy}", new DateTime(year, month, 1));

        ModeHintLabel.Text = IsChoosingDueDate
            ? "Choose a date from your calendar for the deadline"
            : "Tap a date to manage your plan";

        bool isLandscape = Width > Height;

        double buttonHeight = isLandscape ? 72 : 58;
        double buttonWidth = isLandscape ? 72 : 58;
        double fontSize = isLandscape ? 19 : 16;

        for (int day = 1; day <= daysInMonth; day++)
        {
            DateTime currentDate = new DateTime(year, month, day);
            string fullDateKey = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}", currentDate);

            bool isToday =
                (year == todayDate.Year) &&
                (month == todayDate.Month) &&
                (day == todayDate.Day);

            bool hasTask = HasTasksForDate(fullDateKey);

            string buttonText = hasTask
                ? $"{day}\n•"
                : day.ToString();

            Button btn = new Button
            {
                Text = buttonText,
                HeightRequest = buttonHeight,
                WidthRequest = buttonWidth,
                MinimumWidthRequest = buttonWidth,
                MinimumHeightRequest = buttonHeight,
                CornerRadius = 22,
                FontSize = fontSize,
                Padding = new Thickness(0),
                LineBreakMode = LineBreakMode.WordWrap,

                // main date button colours
                BackgroundColor = isToday
                    ? Color.FromArgb("#FFE8A8")
                    : hasTask
                        ? Color.FromArgb("#FFE4EE")
                        : Color.FromArgb("#FFF9FC"),

                TextColor = Color.FromArgb("#744A6D"),

                BorderColor = isToday
                    ? Color.FromArgb("#E2BC54")
                    : hasTask
                        ? Color.FromArgb("#F2A1C0")
                        : Color.FromArgb("#EBC8D8"),

                BorderWidth = 1.3
            };

            btn.CommandParameter = fullDateKey;
            btn.Clicked += OnDateClicked;

            int position = day + startDay - 1;
            int row = position / 7;
            int col = position % 7;

            CalendarGrid.Add(btn, col, row);
        }
    }

    private bool HasTasksForDate(string dateKey)
    {
        try
        {
            if (!File.Exists(StorageFilePath))
                return false;

            string json = File.ReadAllText(StorageFilePath);

            using JsonDocument document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(dateKey, out JsonElement dateTasks))
                return false;

            return dateTasks.ValueKind == JsonValueKind.Array && dateTasks.GetArrayLength() > 0;
        }
        catch
        {
            return false;
        }
    }

    private async void OnDateClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string selectedDate)
        {
            // if TaskPage is waiting for a due date, send it back
            if (IsChoosingDueDate)
            {
                string startDateKey = PendingStartDateKey ?? selectedDate;
                string draftTitle = PendingDraftTitle ?? string.Empty;
                string draftCategory = PendingDraftCategory ?? "Deadline";
                string draftPriority = PendingDraftPriority ?? "Medium";

                // Clear temporary due date selection state
                IsChoosingDueDate = false;
                DueDateTargetPage = null;
                PendingStartDateKey = null;
                PendingDraftTitle = null;
                PendingDraftCategory = null;
                PendingDraftPriority = null;

                // Create a new TaskPage instead of reusing the old popped page
                await Navigation.PushAsync(
                    new TaskPage(startDateKey, selectedDate, draftTitle, draftCategory, draftPriority)
                );

                return;
            }

            await Navigation.PushAsync(new TaskPage(selectedDate));
        }
    }

    private void PreviousMonth(object? sender, EventArgs e)
    {
        currentMonth--;

        if (currentMonth < 1)
        {
            currentMonth = 12;
            currentYear--;
        }

        GenerateCalendar();
    }

    private void NextMonth(object? sender, EventArgs e)
    {
        currentMonth++;

        if (currentMonth > 12)
        {
            currentMonth = 1;
            currentYear++;
        }

        GenerateCalendar();
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        UpdateResponsiveLayout();
        GenerateCalendar();
    }

    private void UpdateResponsiveLayout()
    {
        if (Width <= 0 || Height <= 0)
            return;

        bool isLandscape = Width > Height;

        RootGrid.RowDefinitions.Clear();
        RootGrid.ColumnDefinitions.Clear();

        RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        Grid.SetRow(HeaderSection, 0);
        Grid.SetColumn(HeaderSection, 0);

        HeaderSection.HorizontalOptions = LayoutOptions.Center;
        HeaderSection.VerticalOptions = LayoutOptions.Start;
        MonthNavigation.HorizontalOptions = LayoutOptions.Center;

        if (isLandscape)
        {
            RootGrid.Padding = new Thickness(30, 20);
            HeaderSection.Spacing = 12;
            CalendarSection.Spacing = 14;
            CalendarGrid.ColumnSpacing = 12;
            CalendarGrid.RowSpacing = 10;
        }
        else
        {
            RootGrid.Padding = new Thickness(22, 22);
            HeaderSection.Spacing = 14;
            CalendarSection.Spacing = 14;
            CalendarGrid.ColumnSpacing = 8;
            CalendarGrid.RowSpacing = 9;
        }
    }
}