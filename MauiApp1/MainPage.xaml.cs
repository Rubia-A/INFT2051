using System.Data;

namespace MauiApp1;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        GenerateCalendar();
    }

    void GenerateCalendar()
    {
        int daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);
        int today =DateTime.Now.Day; 

        for (int day = 1; day <= daysInMonth; day++)
        {
            Button btn = new Button
            {
                Text = day.ToString(),
                HeightRequest = 60,
                CornerRadius = 12,
                BackgroundColor = day == today
                        ? Colors.White : Color.FromArgb("#9D8BD9")
            };
            btn.Clicked += OnDateClicked;

            int row = (day - 1) / 7;
            int col = (day - 1) % 7;

            CalendarGrid.Add(btn,col,row);
        }
    }
    async void OnDateClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        string selectedDate = button.Text;

        await Navigation.PushAsync(new TaskPage(selectedDate));
    }
}
