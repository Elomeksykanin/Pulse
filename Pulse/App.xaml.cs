using System.Windows;
using System.Windows.Media.Animation;
using System.IO;
using System.Text.Json;
using Velopack;

namespace Pulse;

public partial class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        var application = new App();
        application.InitializeComponent();
        application.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        GamePreferences preferences;
        try
        {
            preferences = GamePreferences.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            preferences = new GamePreferences();
            MessageBox.Show(
                $"Nie udało się odczytać ustawień Pulse. Zostanie użyty język polski.\n{ex.Message}",
                "Ustawienia Pulse", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        var language = preferences.Language is "en" ? "en" : "pl";
        var splash = new SplashWindow(language);
        splash.EnterRequested += (_, _) =>
        {
            var main = new MainWindow(preferences);
            MainWindow = main;
            main.Opacity = 0;
            main.Show();
            main.BeginAnimation(Window.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(520))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        };
        splash.Show();
    }
}
