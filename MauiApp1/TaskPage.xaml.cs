using System.Diagnostics;

namespace MauiApp1;

public partial class TaskPage : ContentPage
{
    Dictionary<string, List<string>> taskData = new Dictionary<string, List<string>>()
    {
        { "15", new List<string> { "Finish Assessment1", "Study C#" } },
        { "16", new List<string> { "Finish Reflection", "Study figma" } }
    };

    public TaskPage(string date)
    {
        InitializeComponent();
        TitleLabel.Text = "Tasks for " + date;

        if (taskData.ContainsKey(date))
        {
            foreach (var task in taskData[date])
            {
                TaskList.Children.Add(new Label
                {
                    Text = "• " + task,
                    TextColor = Colors.White
                });
            }
        }
        else
        {
            TaskList.Children.Add(new Label
            {
                Text = "No tasks for this date",
                TextColor = Colors.Gray
            });
        }
    }
}