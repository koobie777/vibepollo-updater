namespace VibepolloUi;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window { Title = "Vibepollo Updater" };
        MainWindow.Content = new MainPage();
        try { MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 820)); } catch { }
        MainWindow.Activate();
    }
}
