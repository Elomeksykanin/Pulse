using System.Windows;

namespace Pulse;

public partial class UpdateDialog : Window
{
    private TaskCompletionSource<bool>? _decision;
    private bool _downloadInProgress;

    public UpdateDialog(string version, string releaseNotes)
    {
        InitializeComponent();
        Closing += (_, e) =>
        {
            if (_downloadInProgress)
                e.Cancel = true;
        };
        Closed += (_, _) => _decision?.TrySetResult(false);
        VersionText.Text = version;
        ReleaseNotesText.Text = string.IsNullOrWhiteSpace(releaseNotes)
            ? "Brak opisu zmian dla tej wersji."
            : releaseNotes.Trim();
    }

    public Task<bool> WaitForDownloadDecisionAsync()
    {
        _decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _decision.Task;
    }

    public void SetProgress(int percent)
    {
        var progress = Math.Clamp(percent, 0, 100);
        DownloadProgressPanel.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = progress;
        ProgressPercent.Text = $"{progress}%";
        ProgressLabel.Text = "Pobieranie aktualizacji…";
        ErrorText.Visibility = Visibility.Collapsed;
    }

    public void ShowDownloadError(string message)
    {
        _downloadInProgress = false;
        DownloadProgressPanel.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 0;
        ProgressPercent.Text = "0%";
        ProgressLabel.Text = "Nie udało się pobrać aktualizacji.";
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        DialogSubtitle.Text = "Sprawdź połączenie i spróbuj ponownie.";
        UpdateActionButton.Content = "Ponów";
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
        ProgressLabel.Text = "Pobieranie aktualizacji…";
        DialogSubtitle.Text = "Pobieranie aktualizacji…";
        _decision?.TrySetResult(true);
    }

    private void OnLaterClick(object sender, RoutedEventArgs e)
    {
        _decision?.TrySetResult(false);
        Close();
    }
}
