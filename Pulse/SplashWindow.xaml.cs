using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pulse;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        var version = typeof(SplashWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        VersionLabel.Text = $"UWAGA  •  WERSJA BETA {version}";
    }

    public event EventHandler? EnterRequested;

    private async void EnterApp(object sender, RoutedEventArgs e)
    {
        EnterButton.IsEnabled = false;
        EnterButton.Content = "Uruchamiam…";
        EnterRequested?.Invoke(this, EventArgs.Empty);

        var duration = TimeSpan.FromMilliseconds(420);
        var completion = new TaskCompletionSource();
        var fade = new DoubleAnimation(0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) => completion.TrySetResult();
        SplashCard.BeginAnimation(OpacityProperty, fade);

        if (SplashCard.RenderTransform is ScaleTransform scale)
        {
            var zoom = new DoubleAnimation(1.035, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, zoom);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, zoom);
        }

        await completion.Task;
        Close();
    }
}
