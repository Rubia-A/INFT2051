namespace MauiApp1;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OpenTaskPage(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new TaskPage());
    }
}
