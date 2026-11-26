using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pulse;

public partial class SplashWindow : Window
{
    private string _language;

    public SplashWindow(string language)
    {
        InitializeComponent();
        _language = language is "en" ? "en" : "pl";
        ApplyLanguage();
        var version = typeof(SplashWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        VersionLabel.Text = _language == "en"
            ? $"NOTE  •  BETA VERSION {version}"
            : $"UWAGA  •  WERSJA BETA {version}";
    }

    public event EventHandler? EnterRequested;
    public event Action<string>? LanguageChanged;

    private void SelectPolish(object sender, RoutedEventArgs e) => SetLanguage("pl");

    private void SelectEnglish(object sender, RoutedEventArgs e) => SetLanguage("en");

    private void SetLanguage(string language)
    {
        if (_language == language)
            return;

        _language = language;
        ApplyLanguage();
        LanguageChanged?.Invoke(_language);
    }

    private void ApplyLanguage()
    {
        UiLanguage.Apply(this, _language);
        PolishLanguageButton.Background = new SolidColorBrush(
            _language == "pl" ? Color.FromRgb(89, 66, 168) : Color.FromRgb(37, 34, 58));
        EnglishLanguageButton.Background = new SolidColorBrush(
            _language == "en" ? Color.FromRgb(89, 66, 168) : Color.FromRgb(37, 34, 58));
    }

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
