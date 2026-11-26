using System.Windows;

namespace Pulse;

public partial class UpdateDialog : Window
{
    private readonly string _language;
    private TaskCompletionSource<bool>? _decision;
    private bool _downloadInProgress;

    public UpdateDialog(string version, string releaseNotes, string language)
    {
        InitializeComponent();
        Closed += (_, _) => _decision?.TrySetResult(false);
        Closing += (_, e) =>
        {
            if (_downloadInProgress)
                e.Cancel = true;
        };
        _language = language;
        UiLanguage.Apply(this, language);
        VersionText.Text = version;
        ReleaseNotesText.Text = string.IsNullOrWhiteSpace(releaseNotes)
            ? UiLanguage.Get("Brak opisu zmian dla tej wersji.", language)
            : releaseNotes.Trim();
    }

    public Task<bool> WaitForDownloadDecisionAsync()
    {
        _decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _decision.Task;
    }

    public void SetProgress(int percent)
    {
        DownloadProgressPanel.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = Math.Clamp(percent, 0, 100);
        ProgressPercent.Text = $"{Math.Clamp(percent, 0, 100)}%";
        ProgressLabel.Text = UiLanguage.Get("Pobieranie aktualizacji…", _language);
        ErrorText.Visibility = Visibility.Collapsed;
    }

    public void ShowDownloadError(string message)
    {
        _downloadInProgress = false;
        DownloadProgressPanel.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 0;
        ProgressPercent.Text = "0%";
        ProgressLabel.Text = UiLanguage.Get("Nie udało się pobrać aktualizacji.", _language);
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        DialogSubtitle.Text = UiLanguage.Get("Sprawdź połączenie i spróbuj ponownie.", _language);
        UpdateActionButton.Content = UiLanguage.Get("Ponów", _language);
        UpdateActionButton.IsEnabled = true;
        LaterActionButton.IsEnabled = true;
        LaterButton.IsEnabled = true;
    }

    public void CloseForRestart()
    {
        _downloadInProgress = false;
        Close();
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        _downloadInProgress = true;
        UpdateActionButton.IsEnabled = false;
        LaterActionButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        DownloadProgressPanel.Visibility = Visibility.Visible;
        ErrorText.Visibility = Visibility.Collapsed;
        ProgressLabel.Text = UiLanguage.Get("Pobieranie aktualizacji…", _language);
        DialogSubtitle.Text = UiLanguage.Get("Pobieranie aktualizacji…", _language);
        _decision?.TrySetResult(true);
    }

    private void OnLaterClick(object sender, RoutedEventArgs e)
    {
        _decision?.TrySetResult(false);
        Close();
    }
}
