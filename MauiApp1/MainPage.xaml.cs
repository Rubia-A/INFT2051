using System.Data;

namespace MauiApp1;

public partial class MainPage : ContentPage
{
    int currentYear;
    int currentMonth;

    public MainPage()
    {
        InitializeComponent();
        currentYear = DateTime.Now.Year;
        currentMonth = DateTime.Now.Month;

        GenerateCalendar();
    }

    void GenerateCalendar()
    {
        CalendarGrid.Children.Clear();

        int year = currentYear;
        int month = currentMonth;

        int daysInMonth = DateTime.DaysInMonth(year, month);
        
        DateTime todayDate = DateTime.Now;
        int today = todayDate.Day;

        DateTime firstDay = new DateTime(year, month, 1);
        int startDay = (int)firstDay.DayOfWeek;

        startDay = (startDay == 0) ? 6 : startDay - 1;

        MonthLabel.Text = new DateTime(year, month, 1).ToString("MMMM yyyy");

        for (int day = 1; day <= daysInMonth; day++)
        {
            bool isToday = (year == todayDate.Year &&
                           month == todayDate.Month &&
                           day == todayDate.Day);

            Button btn = new Button
            {
                Text = day.ToString(),
                HeightRequest = 60,
                CornerRadius = 12,
                BackgroundColor = isToday
                    ? Colors.Orange
                    : Color.FromArgb("#9D8BD9")
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
    async void OnDateClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string selectedDate)
        {
            await Navigation.PushAsync(new TaskPage(selectedDate));
        }
    }
    void PreviousMonth(object sender, EventArgs e)
    {
        currentMonth--;

        if (currentMonth < 1)
        {
            currentMonth = 12;
            currentYear--;
        }

        GenerateCalendar();
    }
      
    void NextMonth(object sender, EventArgs e)
    {
        currentMonth++;

        if (currentMonth > 12)
        {
            currentMonth = 1;
            currentYear++;
        }

        GenerateCalendar();
    }
}
