using System.Data;

namespace MauiApp1;

public partial class MainPage : ContentPage
{
    private int currentYear;
    private int currentMonth;

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

    private void GenerateCalendar()
    {
        CalendarGrid.Children.Clear();

        int year = currentYear;
        int month = currentMonth;

        int daysInMonth = DateTime.DaysInMonth(year, month);
        DateTime todayDate = DateTime.Now;

        DateTime firstDay = new DateTime(year, month, 1);
        int startDay = (int)firstDay.DayOfWeek;

        // Convert Sunday-first to Monday-first
        startDay = (startDay == 0) ? 6 : startDay - 1;

        MonthLabel.Text = new DateTime(year, month, 1).ToString("MMMM yyyy");

        bool isLandscape = Width > Height;

        double buttonHeight = isLandscape ? 54 : 44;
        double buttonWidth = isLandscape ? 54 : 44;
        double fontSize = isLandscape ? 18 : 14;

        for (int day = 1; day <= daysInMonth; day++)
        {
            bool isToday =
                (year == todayDate.Year) &&
                (month == todayDate.Month) &&
                (day == todayDate.Day);

            Button btn = new Button
            {
                Text = day.ToString(),
                HeightRequest = buttonHeight,
                WidthRequest = buttonWidth,
                MinimumWidthRequest = buttonWidth,
                MinimumHeightRequest = buttonHeight,
                CornerRadius = 12,
                FontSize = fontSize,
                Padding = new Thickness(0),
                BackgroundColor = isToday
           ? Colors.White
           : Color.FromArgb("#9D8BD9"),
                TextColor = Colors.Black
            };

            string fullDateKey = new DateTime(year, month, day).ToString("yyyy-MM-dd");
            btn.CommandParameter = fullDateKey;

            btn.Clicked += OnDateClicked;

            int position = day + startDay - 1;
            int row = position / 7;
            int col = position % 7;

            CalendarGrid.Add(btn, col, row);
        }
    }

    private async void OnDateClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string selectedDate)
        {
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

        Grid.SetRow(CalendarSection, 1);
        Grid.SetColumn(CalendarSection, 0);

        HeaderSection.HorizontalOptions = LayoutOptions.Center;
        HeaderSection.VerticalOptions = LayoutOptions.Start;
        MonthNavigation.HorizontalOptions = LayoutOptions.Center;
        CalendarSection.HorizontalOptions = LayoutOptions.Center;

        if (isLandscape)
        {
            RootGrid.Padding = new Thickness(20, 18);
            HeaderSection.Spacing = 12;
            CalendarSection.Spacing = 12;
            CalendarGrid.ColumnSpacing = 12;
            CalendarGrid.RowSpacing = 12;
        }
        else
        {
            RootGrid.Padding = new Thickness(24, 24);
            HeaderSection.Spacing = 16;
            CalendarSection.Spacing = 16;
            CalendarGrid.ColumnSpacing = 6;
            CalendarGrid.RowSpacing = 8;
        }
    }
}
