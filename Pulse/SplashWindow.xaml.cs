using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pulse;

public partial class SplashWindow : Window
{
    private readonly string _language;

    public SplashWindow(string language)
    {
        InitializeComponent();
        _language = language;
        UiLanguage.Apply(this, _language);
        var version = typeof(SplashWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        VersionLabel.Text = _language == "en"
            ? $"NOTE  •  BETA VERSION {version}"
            : $"UWAGA  •  WERSJA BETA {version}";
    }

    public event EventHandler? EnterRequested;

    private async void EnterApp(object sender, RoutedEventArgs e)
    {
        EnterButton.IsEnabled = false;
        EnterButton.Content = UiLanguage.Get("Uruchamiam…", _language);
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
