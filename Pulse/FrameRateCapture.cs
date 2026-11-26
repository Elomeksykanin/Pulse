using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Pulse;

internal sealed class FrameRateCapture : IDisposable
{
    private readonly string _filePath;
    private readonly string _language;
    private readonly Process? _process;
    private int _lastLine;
    private string? _error;

    public FrameRateCapture(int processId, string language)
    {
        _language = language;
        _filePath = string.Empty;
        _process = null;
        var executable = Path.Combine(AppContext.BaseDirectory, "Resources", "PresentMon.exe");
        if (!File.Exists(executable))
        {
            _error = T("Brak modułu przechwytywania FPS (PresentMon.exe).",
                "The FPS capture module (PresentMon.exe) is missing.");
            return;
        }

        _filePath = Path.Combine(Path.GetTempPath(), $"pulse-{Environment.ProcessId}-{Guid.NewGuid():N}.csv");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        start.ArgumentList.Add("--session_name");
        start.ArgumentList.Add($"Pulse-{Environment.ProcessId}-{Guid.NewGuid():N}");
        start.ArgumentList.Add("--process_id");
        start.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--output_file");
        start.ArgumentList.Add(_filePath);
        start.ArgumentList.Add("--no_console_stats");
        start.ArgumentList.Add("--no_track_input");
        start.ArgumentList.Add("--terminate_on_proc_exit");

        try
        {
            _process = Process.Start(start);
            if (_process is null) _error = T("Nie udało się uruchomić przechwytywania FPS.",
                "Could not start FPS capture.");
        }
        catch (Exception ex)
        {
            _error = ex is System.ComponentModel.Win32Exception { NativeErrorCode: 1223 }
                ? T("Nie udzielono zgody administratora; FPS nie będą dostępne.",
                    "Administrator permission was not granted; FPS data will be unavailable.")
                : $"{T("Nie udało się uruchomić przechwytywania FPS:", "Could not start FPS capture:")} {ex.Message}";
        }
    }

    public string? Error
    {
        get
        {
            if (_error is not null || _process is not { HasExited: true }) return _error;
            return _process.ExitCode == 6
                ? T("PresentMon nie mógł uruchomić sesji ETW. Sprawdź uprawnienia i konflikty z innymi narzędziami monitorującymi.",
                    "PresentMon could not start an ETW session. Check permissions and conflicts with other monitoring tools.")
                : T($"PresentMon zakończył pracę bez danych (kod {_process.ExitCode}).",
                    $"PresentMon exited without data (code {_process.ExitCode}).");
        }
    }

    public List<FrameSample> ReadNewSamples()
    {
        var samples = new List<FrameSample>();
        if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath)) return samples;

        string text;
        using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream, Encoding.UTF8))
            text = reader.ReadToEnd();

        var complete = text.LastIndexOf('\n');
        if (complete < 0) return samples;
        var lines = text[..complete].Split('\n');
        if (lines.Length < 2) return samples;

        var header = lines[0].TrimStart('\uFEFF').TrimEnd('\r').Split(',');
        var frameIndex = FindColumn(header, "FrameTime", "MsBetweenPresents");
        var timeIndex = FindColumn(header, "CPUStartTimeInMs", "TimeInMs", "CPUStartTime", "CPUStartTimeInSeconds", "TimeInSeconds");
        var timeColumn = timeIndex >= 0 ? header[timeIndex] : string.Empty;
        var timeIsSeconds = timeColumn.Equals("CPUStartTime", StringComparison.OrdinalIgnoreCase) ||
                            timeColumn.Contains("Seconds", StringComparison.OrdinalIgnoreCase);
        var gpuIndex = FindColumn(header, "GPUBusy", "MsGPUBusy");
        var chainIndex = FindColumn(header, "SwapChainAddress");

        if (frameIndex < 0)
        {
            _error = T("PresentMon zwrócił nieznany format danych FPS.",
                "PresentMon returned an unrecognized FPS data format.");
            return samples;
        }

        for (var i = Math.Max(1, _lastLine); i < lines.Length; i++)
        {
            var cells = lines[i].TrimEnd('\r').Split(',');
            if (cells.Length <= frameIndex || !TryParse(cells[frameIndex], out var frameTimeMs) ||
                frameTimeMs <= 0 || frameTimeMs > 10_000) continue;

            var timestampMs = (i - 1) * frameTimeMs;
            if (timeIndex >= 0 && cells.Length > timeIndex && TryParse(cells[timeIndex], out var timestamp))
                timestampMs = timeIsSeconds ? timestamp * 1000 : timestamp;

            double? gpuBusyMs = gpuIndex >= 0 && cells.Length > gpuIndex && TryParse(cells[gpuIndex], out var busy)
                ? busy
                : null;
            var swapChain = chainIndex >= 0 && cells.Length > chainIndex ? cells[chainIndex] : "main";
            samples.Add(new FrameSample(timestampMs, frameTimeMs, gpuBusyMs, swapChain));
        }

        _lastLine = lines.Length;
        return samples;
    }

    public void Stop()
    {
        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(true);
                _process.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                _error ??= $"{T("Nie udało się zatrzymać przechwytywania FPS:", "Could not stop FPS capture:")} {ex.Message}";
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _process?.Dispose();
        if (!string.IsNullOrEmpty(_filePath))
        {
            try { File.Delete(_filePath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static int FindColumn(string[] header, params string[] names)
        => Array.FindIndex(header, column => names.Any(name =>
            column.Trim().Trim('"').Equals(name, StringComparison.OrdinalIgnoreCase)));

    private static bool TryParse(string value, out double number)
        => double.TryParse(value.Trim().Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out number);

    private string T(string polish, string english) => _language == "en" ? english : polish;
}

internal sealed record FrameSample(double TimestampMs, double FrameTimeMs, double? GpuBusyMs, string SwapChain);
