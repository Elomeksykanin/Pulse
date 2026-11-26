using System.Windows;
using System.Windows.Media.Animation;
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
        var splash = new SplashWindow();
        splash.EnterRequested += (_, _) =>
        {
            var main = new MainWindow();
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
