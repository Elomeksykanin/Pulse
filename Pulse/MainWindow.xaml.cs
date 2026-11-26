using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace Pulse;

public partial class MainWindow : Window
{
    private readonly UpdateManager _updateManager = new(new GithubSource("https://github.com/Elomeksykanin/Pulse", null, false, null));
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _generalAnalysisTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _ramHistory = new();
    private readonly Dictionary<int, (ulong Time, DateTime Stamp)> _processSamples = new();
    private readonly List<FrameSample> _frames = [];
    private readonly List<double> _gameCpuSamples = [];
    private readonly List<double> _systemCpuSamples = [];
    private readonly List<double> _ramSamples = [];
    private readonly List<double> _cpuTemperatureSamples = [];
    private readonly List<double> _gpuTemperatureSamples = [];
    private readonly List<long> _pingSamples = [];
    private readonly List<SystemSample> _generalSystemSamples = [];
    private readonly Dictionary<ProcessKey, ProcessUsageAccumulator> _generalProcesses = [];
    private readonly Dictionary<ProcessKey, (TimeSpan Cpu, DateTime CapturedAt)> _generalPreviousProcessSamples = [];
    private FrameRateCapture? _frameRateCapture;
    private string? _captureError;
    private HardwareSensors? _hardwareSensors;
    private DateTime _sessionStarted;
    private bool _sampleBusy;
    private bool _generalSampleBusy;
    private bool _generalAnalysisRunning;
    private bool _updateCheckRunning;
    private DateTime _generalAnalysisStartedAt;
    private TimeSpan _generalAnalysisDuration;
    private double? _lastGpuBusyPercent;
    private IReadOnlyList<GameWindowInfo> _games = [];
    private GameWindowInfo? _selectedGame;
    private bool _chartExpanded;
    private Visibility _sessionPanelVisibility;
    private Visibility _resultsPanelVisibility;
    private Visibility _summaryPanelVisibility;
    private Visibility _awaitingResultsPanelVisibility;

    public MainWindow()
    {
        InitializeComponent();
        AppVersionText.Text = $"Wersja Beta {typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.3"}";
        SetNavigation(NavOverview);
        SetResultsVisibility(false, false);
        _timer.Tick += async (_, _) => await SampleAsync();
        _generalAnalysisTimer.Tick += async (_, _) => await SampleGeneralAnalysis();
        Loaded += (_, _) => RefreshGames(null, null);
        Loaded += async (_, _) => await CheckForUpdatesAsync(false);
        Closing += (_, _) =>
        {
            _timer.Stop();
            _generalAnalysisTimer.Stop();
            _frameRateCapture?.Dispose();
            try { _hardwareSensors?.Dispose(); } catch { }
        };
    }

    private async void CheckForUpdates(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(true);
    }

    private async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (_updateCheckRunning)
            return;

        if (!_updateManager.IsInstalled)
        {
            if (userInitiated)
            {
                var result = MessageBox.Show(
                    this,
                    "Automatyczne aktualizacje działają po zainstalowaniu Pulse. Otworzyć stronę wydań, aby pobrać instalator?",
                    "Zainstaluj Pulse",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo("https://github.com/Elomeksykanin/Pulse/releases/latest") { UseShellExecute = true });
            }
            return;
        }

        _updateCheckRunning = true;
        UpdateButton.IsEnabled = false;
        try
        {
            StatusText.Text = "●  SPRAWDZAM AKTUALIZACJE";
            var update = await _updateManager.CheckForUpdatesAsync();
            if (update is null)
            {
                StatusText.Text = "●  APLIKACJA JEST AKTUALNA";
                if (userInitiated)
                    MessageBox.Show(this, "Masz najnowszą wersję Pulse.", "Aktualizacje", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var version = update.TargetFullRelease.Version;
            var result = MessageBox.Show(
                this,
                $"Dostępna jest wersja {version}. Pobrać aktualizację i uruchomić Pulse ponownie?",
                "Aktualizacja Pulse",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (result != MessageBoxResult.Yes)
            {
                StatusText.Text = "●  DOSTĘPNA AKTUALIZACJA";
                return;
            }

            StatusText.Text = "●  POBIERAM AKTUALIZACJĘ";
            await _updateManager.DownloadUpdatesAsync(update, percent =>
                Dispatcher.InvokeAsync(() => StatusText.Text = $"●  POBIERAM AKTUALIZACJĘ {percent}%"));
            StatusText.Text = "●  PONOWNIE URUCHAMIAM";
            _updateManager.ApplyUpdatesAndRestart(update.TargetFullRelease);
        }
        catch (Exception ex)
        {
            StatusText.Text = "●  NIE UDAŁO SIĘ SPRAWDZIĆ AKTUALIZACJI";
            if (userInitiated)
                MessageBox.Show(this, $"Nie udało się sprawdzić lub zainstalować aktualizacji:\n{ex.Message}", "Błąd aktualizacji", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _updateCheckRunning = false;
            UpdateButton.IsEnabled = true;
        }
    }

    private void ShowOverview(object sender, RoutedEventArgs e)
    {
        OverviewPage.Visibility = Visibility.Visible;
        GeneralAnalysisPage.Visibility = Visibility.Collapsed;
        MonitorPage.Visibility = Visibility.Collapsed;
        SetNavigation(NavOverview);
    }

    private void ShowGeneralAnalysis(object sender, RoutedEventArgs e)
    {
        OverviewPage.Visibility = Visibility.Collapsed;
        GeneralAnalysisPage.Visibility = Visibility.Visible;
        MonitorPage.Visibility = Visibility.Collapsed;
        GeneralAnalysisPage.ScrollToTop();
        SetNavigation(NavGeneral);
    }

    private void ShowMonitor(object sender, RoutedEventArgs e)
    {
        MonitorPage.Visibility = Visibility.Visible;
        GeneralAnalysisPage.Visibility = Visibility.Collapsed;
        OverviewPage.Visibility = Visibility.Collapsed;
        SetNavigation(NavMonitor);
        DrawChart();
    }

    private void SetNavigation(Button activeButton)
    {
        SetNavigationButton(NavOverview, ReferenceEquals(activeButton, NavOverview));
        SetNavigationButton(NavGeneral, ReferenceEquals(activeButton, NavGeneral));
        SetNavigationButton(NavMonitor, ReferenceEquals(activeButton, NavMonitor));
    }

    private void SetNavigationButton(Button button, bool isActive)
    {
        button.Foreground = (Brush)FindResource(isActive ? "Accent" : "Muted");
        button.Background = new SolidColorBrush(Color.FromRgb(
            isActive ? (byte)45 : (byte)16,
            isActive ? (byte)37 : (byte)16,
            isActive ? (byte)72 : (byte)25));
    }

    private async void StartGeneralAnalysis(object sender, RoutedEventArgs e)
    {
        if (_timer.IsEnabled)
        {
            MessageBox.Show(this, "Zakończ najpierw pomiar gry, aby nie mieszać wyników obu analiz.", "Analiza już działa", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!int.TryParse(GeneralDurationBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) || minutes is < 1 or > 120)
        {
            GeneralStatusText.Text = "Wpisz czas od 1 do 120 minut.";
            GeneralDurationBox.Focus();
            return;
        }

        _generalSystemSamples.Clear();
        _generalProcesses.Clear();
        _generalPreviousProcessSamples.Clear();
        _staticLast = null;
        _generalAnalysisStartedAt = DateTime.UtcNow;
        _generalAnalysisDuration = TimeSpan.FromMinutes(minutes);
        _generalAnalysisRunning = true;
        GeneralDurationBox.IsEnabled = false;
        GeneralStartButton.IsEnabled = false;
        GeneralStopButton.IsEnabled = true;
        GeneralProgress.Value = 0;
        RenderGeneralReport("Zbieram próbki. Używaj komputera tak jak zwykle; nic nie zostanie automatycznie zamknięte.");
        GeneralStatusText.Text = $"Analiza trwa {minutes} min. Próbkowanie co 5 sekund.";
        StatusText.Text = "●  ANALIZA OGÓLNA";
        StatusText.Foreground = (Brush)FindResource("Accent");
        _generalAnalysisTimer.Start();

        await SampleGeneralAnalysis();
    }

    private async void StopGeneralAnalysis(object sender, RoutedEventArgs e)
    {
        _generalAnalysisTimer.Stop();
        GeneralStopButton.IsEnabled = false;
        while (_generalSampleBusy) await Task.Delay(50);
        CompleteGeneralAnalysis();
    }

    private async Task SampleGeneralAnalysis()
    {
        if (!_generalAnalysisRunning || _generalSampleBusy) return;
        _generalSampleBusy = true;
        GeneralStopButton.IsEnabled = false;
        try
        {
            var sample = await Task.Run(ReadGeneralAnalysisSample);
            _generalSystemSamples.Add(sample.System);

            foreach (var process in sample.Processes)
            {
                if (!_generalProcesses.TryGetValue(process.Key, out var usage))
                {
                    usage = new ProcessUsageAccumulator(process.Key, process.Name, process.HasVisibleWindow);
                    _generalProcesses.Add(process.Key, usage);
                }
                usage.AddMemory(process.WorkingSetBytes, process.HasVisibleWindow);

                if (_generalPreviousProcessSamples.TryGetValue(process.Key, out var previous))
                {
                    var elapsed = (sample.CapturedAt - previous.CapturedAt).TotalSeconds;
                    if (elapsed > 0 && process.CpuTime >= previous.Cpu)
                    {
                        var cpuPercent = (process.CpuTime - previous.Cpu).TotalSeconds / elapsed
                                         / Math.Max(1, Environment.ProcessorCount) * 100;
                        usage.AddCpu(Math.Clamp(cpuPercent, 0, 100));
                    }
                }
                _generalPreviousProcessSamples[process.Key] = (process.CpuTime, sample.CapturedAt);
            }

            var elapsedTime = DateTime.UtcNow - _generalAnalysisStartedAt;
            GeneralProgress.Value = Math.Clamp(elapsedTime.TotalSeconds / _generalAnalysisDuration.TotalSeconds, 0, 1);
            var remaining = _generalAnalysisDuration - elapsedTime;
            var remainingText = remaining.TotalHours >= 1 ? remaining.ToString(@"h\:mm\:ss") : remaining.ToString(@"m\:ss");
            GeneralStatusText.Text = $"Analiza trwa • pozostało {remainingText} • CPU {sample.System.Cpu?.ToString("0", CultureInfo.InvariantCulture) ?? "—"}% • RAM {sample.System.RamPercent}%";

            if (elapsedTime >= _generalAnalysisDuration)
                CompleteGeneralAnalysis();
        }
        catch (Exception ex)
        {
            _generalAnalysisTimer.Stop();
            _generalAnalysisRunning = false;
            RenderGeneralReport("Analiza została przerwana, bo nie udało się odczytać danych systemowych:\n" + ex.Message);
            GeneralStatusText.Text = "Nie udało się dokończyć analizy.";
            StatusText.Text = "●  BŁĄD ANALIZY";
            StatusText.Foreground = Brushes.Salmon;
            SetGeneralAnalysisControls(false);
        }
        finally
        {
            _generalSampleBusy = false;
            if (_generalAnalysisRunning) GeneralStopButton.IsEnabled = true;
        }
    }

    private void CompleteGeneralAnalysis()
    {
        if (!_generalAnalysisRunning) return;
        _generalAnalysisRunning = false;
        _generalAnalysisTimer.Stop();
        GeneralProgress.Value = Math.Clamp((DateTime.UtcNow - _generalAnalysisStartedAt).TotalSeconds / _generalAnalysisDuration.TotalSeconds, 0, 1);
        RenderGeneralReport(BuildGeneralAnalysisReport());
        GeneralStatusText.Text = $"Analiza zakończona • {_generalSystemSamples.Count} próbek systemu • czas: {(DateTime.UtcNow - _generalAnalysisStartedAt).TotalMinutes:0.0} min.";
        StatusText.Text = "●  ANALIZA ZAKOŃCZONA";
        StatusText.Foreground = (Brush)FindResource("Accent");
        SetGeneralAnalysisControls(false);
    }

    private void SetGeneralAnalysisControls(bool running)
    {
        GeneralDurationBox.IsEnabled = !running;
        GeneralStartButton.IsEnabled = !running;
        GeneralStopButton.IsEnabled = running && !_generalSampleBusy;
    }

    private void RenderGeneralReport(string report)
    {
        GeneralReportContent.Children.Clear();
        foreach (var line in report.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                GeneralReportContent.Children.Add(new Border { Height = 8 });
                continue;
            }

            var isHeading = line.StartsWith("PODSUMOWANIE", StringComparison.Ordinal) ||
                            line is "NAJWIĘKSZE UŻYCIE PAMIĘCI" or "NAJWIĘKSZE UŻYCIE CPU" or
                                "PROGRAMY W TLE DO SPRAWDZENIA" or "WNIOSKI";
            var isBullet = line.StartsWith("• ", StringComparison.Ordinal);
            var text = new TextBlock
            {
                Text = line,
                Foreground = (Brush)FindResource(isHeading ? "Accent" : isBullet ? "Ink" : "Muted"),
                FontSize = isHeading ? 11 : 13,
                FontWeight = isHeading ? FontWeights.Bold : FontWeights.Normal,
                LineHeight = 20,
                TextWrapping = TextWrapping.Wrap,
                Margin = isHeading
                    ? new Thickness(0, 3, 0, 2)
                    : isBullet ? new Thickness(8, 0, 0, 0) : new Thickness(0)
            };
            GeneralReportContent.Children.Add(text);
        }
    }

    private string BuildGeneralAnalysisReport()
    {
        if (_generalSystemSamples.Count == 0) return "Nie zebrano żadnych próbek. Uruchom analizę ponownie.";

        var cpuSamples = _generalSystemSamples.Where(x => x.Cpu.HasValue).Select(x => x.Cpu!.Value).ToArray();
        var averageCpu = cpuSamples.Length == 0 ? (double?)null : cpuSamples.Average();
        var averageRam = _generalSystemSamples.Average(x => x.RamPercent);
        var peakRam = _generalSystemSamples.Max(x => x.RamPercent);
        var topMemory = _generalProcesses.Values.OrderByDescending(x => x.AverageWorkingSetMb).Take(5).ToArray();
        var topCpu = _generalProcesses.Values.Where(x => x.CpuSampleCount > 0).OrderByDescending(x => x.AverageCpu).Take(5).ToArray();
        var background = _generalProcesses.Values
            .Where(x => !x.HasVisibleWindow && (x.AverageCpu >= 3 || x.AverageWorkingSetMb >= 750))
            .OrderByDescending(x => Math.Max(x.AverageCpu / 3, x.AverageWorkingSetMb / 750))
            .Take(5)
            .ToArray();

        var lines = new List<string>
        {
            $"PODSUMOWANIE • {(DateTime.UtcNow - _generalAnalysisStartedAt).TotalMinutes:0.0} min",
            $"Średnie użycie RAM: {averageRam:0}% • maksimum: {peakRam}%.",
            averageCpu is null ? "CPU: za mało próbek do wyliczenia średniej." : $"Średnie użycie CPU: {averageCpu:0}%."
        };

        lines.Add("");
        lines.Add("NAJWIĘKSZE UŻYCIE PAMIĘCI");
        lines.Add("Średni zestaw roboczy podczas obserwacji:");
        lines.AddRange(topMemory.Length == 0
            ? ["Nie udało się odczytać pamięci procesów."]
            : topMemory.Select(x => $"• {x.Name} (PID {x.Key.ProcessId}) — średnio {x.AverageWorkingSetMb:0} MB, maksimum {x.PeakWorkingSetMb:0} MB."));

        lines.Add("");
        lines.Add("NAJWIĘKSZE UŻYCIE CPU");
        lines.Add("Średnie użycie całkowitej mocy procesora:");
        lines.AddRange(topCpu.Length == 0
            ? ["Za krótka analiza, aby porównać użycie CPU procesów."]
            : topCpu.Select(x => $"• {x.Name} (PID {x.Key.ProcessId}) — średnio {x.AverageCpu:0.0}%, maksimum {x.PeakCpu:0.0}% całkowitej mocy CPU."));

        lines.Add("");
        lines.Add("PROGRAMY W TLE DO SPRAWDZENIA");
        lines.AddRange(background.Length == 0
            ? ["Nie wykryto procesu bez widocznego okna, który stale przekraczał progi pamięci/CPU."]
            : background.Select(x => $"• {x.Name} (PID {x.Key.ProcessId}) — średnio {x.AverageWorkingSetMb:0} MB RAM i {x.AverageCpu:0.0}% CPU. Sprawdź, czy potrzebujesz go podczas pracy."));

        lines.Add("");
        lines.Add("WNIOSKI");
        if (averageRam >= 85) lines.Add("RAM był często mocno zajęty. Sprawdź wymienione aplikacje, karty przeglądarki i programy uruchomione w tle.");
        else lines.Add("Średnie użycie RAM nie wskazuje na stały brak pamięci w trakcie tej obserwacji.");
        if (averageCpu >= 75) lines.Add("CPU było średnio mocno obciążone; sprawdź procesy z najwyższego zestawienia i powtórz analizę przy typowym obciążeniu.");
        else if (averageCpu is not null) lines.Add("Średnie użycie CPU nie wskazuje na stałe, wysokie obciążenie.");
        lines.Add("To lista procesów zużywających zasoby, nie ocena, czy aplikacja jest zbędna. Nie zamykaj procesów Windows ani sterowników na podstawie samej nazwy; decyzję podejmij samodzielnie.");
        return string.Join("\n", lines);
    }

    private static GeneralAnalysisSample ReadGeneralAnalysisSample()
    {
        var system = ReadSystemSample();
        var capturedAt = DateTime.UtcNow;
        var processes = new List<ProcessUsageSample>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var key = new ProcessKey(process.Id, process.StartTime.ToUniversalTime().Ticks);
                    processes.Add(new ProcessUsageSample(
                        key,
                        process.ProcessName,
                        !string.IsNullOrWhiteSpace(process.MainWindowTitle),
                        process.WorkingSet64,
                        process.TotalProcessorTime));
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                catch (UnauthorizedAccessException) { }
                catch (NotSupportedException) { }
            }
        }
        return new GeneralAnalysisSample(capturedAt, system, processes);
    }

    private async void ScanHardware(object sender, RoutedEventArgs e)
    {
        ScanButton.IsEnabled = false;
        StatusText.Text = "●  SKANOWANIE";
        StatusText.Foreground = (Brush)FindResource("Muted");
        try
        {
            var info = await Task.Run(ReadHardware);
            OverviewSystemValue.Text = info.System;
            OverviewCpuValue.Text = info.Cpu;
            OverviewRamValue.Text = info.Ram;
            OverviewGpuValue.Text = string.Join("\n\n", info.Gpus);
            OverviewDiskValue.Text = string.Join("\n", info.Disks);
            OverviewAdviceText.Text = info.Advice;
            OverviewCompatibilityValue.Text = EvaluateCompatibility(info);
            StatusText.Text = "●  GOTOWY";
            StatusText.Foreground = (Brush)FindResource("Accent");
        }
        catch (Exception ex)
        {
            OverviewSystemValue.Text = "Nie udało się odczytać podzespołów: " + ex.Message;
            StatusText.Text = "●  BŁĄD SKANU";
            StatusText.Foreground = Brushes.Salmon;
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }
    }

    private static string EvaluateCompatibility(HardwareInfo info)
    {
        var observations = new List<string>();
        var gpu = info.GpuModels.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(info.CpuModel) || string.IsNullOrWhiteSpace(gpu))
            observations.Add("Nie udało się rozpoznać modelu procesora lub karty graficznej, więc nie można ich porównać.");
        else if (TryGetCpuTier(info.CpuModel, out var cpuTier) && TryGetGpuTier(gpu, out var gpuTier))
        {
            var difference = cpuTier - gpuTier;
            observations.Add(difference switch
            {
                >= 2 => $"Szacunkowo: {info.CpuModel} powinien mieć zapas względem {gpu} w większości gier. Ograniczeniem częściej będzie karta graficzna.",
                <= -2 => $"Szacunkowo: {info.CpuModel} może ograniczać {gpu}, szczególnie w grach procesorowych i przy wysokim FPS.",
                _ => $"Szacunkowo: {info.CpuModel} i {gpu} są podobnej klasy i powinny tworzyć dość wyrównany zestaw."
            });
        }
        else
            observations.Add($"Nie mam reguły porównawczej dla modeli „{info.CpuModel}” i „{gpu}”, więc nie chcę zgadywać ich balansu.");

        if (info.RamGb < 16)
            observations.Add($"Wykryto {info.RamGb:0.#} GB RAM — 16 GB lub więcej to praktyczny punkt odniesienia do wielu współczesnych gier.");
        if (info.CpuCores < 4)
            observations.Add($"Procesor ma {info.CpuCores} rdzenie — w części gier może ograniczać wydajność.");
        observations.Add("To orientacyjna ocena klas sprzętu, nie pomiar FPS. Wynik zależy od gry, rozdzielczości i ustawień; zasilacz oraz zgodność płyty głównej nie są tu sprawdzane.");
        return string.Join("\n\n", observations);
    }

    private static bool TryGetCpuTier(string model, out int tier)
    {
        var ryzen = Regex.Match(model, @"Ryzen\s+([3579])\s+(\d{4,5})", RegexOptions.IgnoreCase);
        if (ryzen.Success)
        {
            var segment = int.Parse(ryzen.Groups[1].Value, CultureInfo.InvariantCulture);
            var series = int.Parse(ryzen.Groups[2].Value, CultureInfo.InvariantCulture) / 1000;
            tier = segment switch { 3 => 1, 5 => 3, 7 => 4, _ => 5 } + Math.Clamp(series - 5, -1, 1);
            return true;
        }

        var intel = Regex.Match(model, @"\bi([3579])[- ]?(\d{4,5})\b", RegexOptions.IgnoreCase);
        if (intel.Success)
        {
            var segment = int.Parse(intel.Groups[1].Value, CultureInfo.InvariantCulture);
            var number = int.Parse(intel.Groups[2].Value, CultureInfo.InvariantCulture);
            var generation = number / 1000;
            tier = segment switch { 3 => 1, 5 => 3, 7 => 4, _ => 5 } + Math.Clamp(generation - 10, -1, 1);
            return true;
        }

        tier = 0;
        return false;
    }

    private static bool TryGetGpuTier(string model, out int tier)
    {
        var nvidia = Regex.Match(model, @"(?:RTX|GTX)\s*(\d{4})", RegexOptions.IgnoreCase);
        if (nvidia.Success)
        {
            tier = int.Parse(nvidia.Groups[1].Value[^2..], CultureInfo.InvariantCulture) switch
            {
                50 => 2,
                60 => 3,
                70 => 4,
                80 => 5,
                90 => 6,
                _ => 0
            };
            return tier != 0;
        }

        var amd = Regex.Match(model, @"RX\s*(\d{4})", RegexOptions.IgnoreCase);
        if (amd.Success)
        {
            tier = int.Parse(amd.Groups[1].Value[^2..], CultureInfo.InvariantCulture) switch
            {
                50 or 60 => 3,
                70 => 4,
                80 => 5,
                90 => 6,
                _ => 0
            };
            return tier != 0;
        }

        tier = 0;
        return false;
    }

    private static HardwareInfo ReadHardware()
    {
        const string script = @"
$ErrorActionPreference='Stop'
$cpu=Get-CimInstance Win32_Processor | Select-Object -First 1 Name,NumberOfCores,NumberOfLogicalProcessors
$os=Get-CimInstance Win32_OperatingSystem | Select-Object Caption,@{N='RamGB';E={[math]::Round($_.TotalVisibleMemorySize/1MB,1)}}
$gpu=@(Get-CimInstance Win32_VideoController | ForEach-Object {[pscustomobject]@{Name=$_.Name;Driver=$_.DriverVersion;VRAM=if($_.AdapterRAM){[math]::Round($_.AdapterRAM/1GB,1)}else{$null}}})
$disk=@(Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | ForEach-Object {[pscustomobject]@{Name=$_.DeviceID;Size=if($_.Size){[math]::Round($_.Size/1GB,1)}else{$null};Free=if($_.FreeSpace){[math]::Round($_.FreeSpace/1GB,1)}else{$null}}})
[pscustomobject]@{Cpu=$cpu;Os=$os;Gpus=$gpu;Disks=$disk} | ConvertTo-Json -Depth 5 -Compress";
        var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        using var process = new Process { StartInfo = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encodedScript) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(25_000)) { process.Kill(true); throw new TimeoutException("Skan przekroczył limit czasu."); }
        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new InvalidOperationException(error.Trim());
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        var cpu = root.GetProperty("Cpu");
        var os = root.GetProperty("Os");
        var ramGb = os.GetProperty("RamGB").GetDouble();
        var cpuModel = GetString(cpu, "Name") ?? "";
        var gpuModels = root.GetProperty("Gpus").EnumerateArray()
            .Select(g => GetString(g, "Name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
        var gpus = root.GetProperty("Gpus").EnumerateArray().Select(g =>
        {
            var name = GetString(g, "Name") ?? "Nieznana karta";
            var vram = GetNumber(g, "VRAM");
            var driver = GetString(g, "Driver");
            return "•  " + name + (vram is > 0 ? $"  ·  VRAM ok. {vram:0.#} GB" : "") + (string.IsNullOrWhiteSpace(driver) ? "" : $"\n   Sterownik {driver}");
        }).ToArray();
        var disks = root.GetProperty("Disks").EnumerateArray().Select(d =>
        {
            var name = GetString(d, "Name") ?? "Dysk";
            var free = GetNumber(d, "Free");
            var size = GetNumber(d, "Size");
            return $"•  {name}     {free:0.#} GB wolne z {size:0.#} GB";
        }).ToArray();
        var advice = new List<string>();
        if (ramGb < 8) advice.Add("Mniej niż 8 GB RAM może powodować doczytywanie i przycięcia.");
        foreach (var d in root.GetProperty("Disks").EnumerateArray())
        {
            var free = GetNumber(d, "Free"); var size = GetNumber(d, "Size");
            if (size > 0 && free / size < .1) advice.Add($"Dysk {GetString(d, "Name")} ma mniej niż 10% wolnego miejsca.");
        }
        advice.Add("Przy spadku FPS porównaj obciążenie CPU i GPU oraz sprawdź temperatury.");
        advice.Add("Wysoki CPU procesu gry przy niskim GPU może wskazywać limit procesora, ale sprawdź także programy działające w tle.");
        advice.Add("Nie wymieniaj części na podstawie samych nazw. Zbierz pomiary w konkretnej grze.");
        var cpuCores = GetNumber(cpu, "NumberOfCores");
        return new HardwareInfo(GetString(os, "Caption") ?? "Windows",
            $"{cpuModel}\nRdzenie: {cpuCores:0}   ·   Wątki: {GetNumber(cpu, "NumberOfLogicalProcessors"):0}",
            $"{ramGb:0.#} GB", ramGb, cpuCores, cpuModel, gpuModels, gpus.Length == 0 ? ["Nie wykryto karty graficznej."] : gpus,
            disks.Length == 0 ? ["Nie wykryto dysków."] : disks, string.Join("\n\n", advice));
    }

    private static string? GetString(JsonElement element, string name) => element.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.ToString() : null;
    private static double GetNumber(JsonElement element, string name) => element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;

    private void RefreshGames(object? sender, RoutedEventArgs? e)
    {
        _games = Process.GetProcesses().Select(p =>
        {
            try { return new GameWindowInfo(p.Id, p.ProcessName, p.MainWindowTitle); }
            catch { return null; }
        }).Where(g => g is { Title.Length: > 0 }).Cast<GameWindowInfo>().OrderBy(g => g.Title).ToArray();
        if (_selectedGame is not null && !_games.Any(g => g.ProcessId == _selectedGame.ProcessId))
        {
            _selectedGame = null;
            UpdateSelectedGame();
        }
        StatusText.Text = _games.Count > 0 ? $"●  GOTOWY · {_games.Count} OKIEN" : "●  GOTOWY";
    }

    private void ChooseGame(object sender, RoutedEventArgs e)
    {
        var picker = new GamePickerWindow(_games) { Owner = this };
        if (picker.ShowDialog() == true)
        {
            _selectedGame = picker.SelectedGame;
            UpdateSelectedGame();
        }
    }

    private void UpdateSelectedGame()
    {
        SelectedGameTitle.Text = _selectedGame?.Title ?? "Nie wybrano gry";
        SelectedGameDetails.Text = _selectedGame is null
            ? "Wybierz aktywne okno gry, aby mierzyć użycie jej procesu."
            : $"Proces {_selectedGame.Name}  ·  PID {_selectedGame.ProcessId}";
    }

    private async void ToggleMonitor(object sender, RoutedEventArgs e)
    {
        if (_generalAnalysisRunning)
        {
            MessageBox.Show(this, "Zakończ najpierw analizę ogólną, aby nie mieszać wyników obu analiz.", "Analiza już działa", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_timer.IsEnabled)
        {
            await StopSessionAsync();
            return;
        }
        if (_selectedGame is null)
        {
            MessageBox.Show(this, "Najpierw wybierz aktywne okno gry. Dzięki temu Pulse odczyta FPS właśnie z tej gry.", "Wybierz grę", MessageBoxButton.OK, MessageBoxImage.Information);
            ChooseGame(sender, e);
            if (_selectedGame is null) return;
        }
        ResetSession();
        _sessionStarted = DateTime.Now;
        SummaryPanel.Visibility = Visibility.Collapsed;
        SummaryText.Text = "";
        SetResultsVisibility(false, true);
        MonitorButton.Content = "Przygotowuję pomiar…";
        MonitorButton.IsEnabled = false;
        StatusText.Text = "●  URUCHAMIAM POMIAR";
        try
        {
            _hardwareSensors = await Task.Run(() => new HardwareSensors());
        }
        catch
        {
            _hardwareSensors = null;
        }
        StartFrameRateCapture();
        _processSamples.Clear();
        _timer.Start();
        MonitorButton.Content = "■  Zakończ pomiar";
        MonitorButton.IsEnabled = true;
        StatusText.Text = "●  POMIAR W TOKU";
        FpsSubMetric.Text = "zbieram klatki…";
        _ = SampleAsync();
    }

    private async Task SampleAsync()
    {
        if (_sampleBusy || !_timer.IsEnabled) return;
        _sampleBusy = true;
        try
        {
            var game = _selectedGame;
            var telemetry = await Task.Run(() =>
            {
                var system = ReadSystemSample();
                var processCpu = game is null ? null : ReadProcessCpu(game.ProcessId);
                SensorReading? sensors = null;
                try { sensors = _hardwareSensors?.Read(); } catch { }
                var frames = _frameRateCapture?.ReadNewSamples() ?? [];
                return new TelemetrySample(system, processCpu, sensors, frames);
            });
            var s = telemetry.System;
            RamMetric.Text = $"{s.RamPercent}%  ·  {s.RamUsed:0.0}/{s.RamTotal:0.0} GB";
            AddHistory(_ramHistory, s.RamPercent);
            if (s.Cpu is double systemCpu)
            {
                _systemCpuSamples.Add(systemCpu);
                CpuSystemMetric.Text = $"System  {systemCpu:0}%";
            }
            if (telemetry.GameCpu is double processCpuValue)
            {
                GameCpuMetric.Text = $"Gra  {processCpuValue:0.0}%";
                _gameCpuSamples.Add(processCpuValue);
            }
            else GameCpuMetric.Text = "Gra  —";
            if (telemetry.Sensors?.CpuCelsius is float cpuTemp)
            {
                CpuTempMetric.Text = $"{cpuTemp:0}°C";
                _cpuTemperatureSamples.Add(cpuTemp);
            }
            else CpuTempMetric.Text = "n/d";
            if (telemetry.Sensors?.GpuCelsius is float gpuTemp)
            {
                GpuTempMetric.Text = $"{gpuTemp:0}°C";
                _gpuTemperatureSamples.Add(gpuTemp);
            }
            else GpuTempMetric.Text = "n/d";
            if (telemetry.Frames.Count > 0) _frames.AddRange(telemetry.Frames);
            UpdateFrameMetrics();
            if (s.Cpu is double cpuValue) AddHistory(_cpuHistory, cpuValue);
            _ramSamples.Add(s.RamPercent);
            while (_cpuHistory.Count > 60) _cpuHistory.Dequeue();
            if (!string.IsNullOrWhiteSpace(PingHost.Text)) await SamplePingAsync(PingHost.Text.Trim());
            DrawChart();
        }
        catch (Exception ex)
        {
            StatusText.Text = "●  BŁĄD POMIARU";
            PingMetric.ToolTip = ex.Message;
        }
        finally { _sampleBusy = false; }
    }

    private void ResetSession()
    {
        _frames.Clear(); _gameCpuSamples.Clear(); _systemCpuSamples.Clear(); _ramSamples.Clear(); _cpuTemperatureSamples.Clear(); _gpuTemperatureSamples.Clear(); _pingSamples.Clear();
        _cpuHistory.Clear(); _ramHistory.Clear(); _staticLast = null;
        _captureError = null; _lastGpuBusyPercent = null;
        _processSamples.Clear();
        FpsMetric.Text = "—"; LowFpsMetric.Text = "—"; GpuBusyMetric.Text = "—";
        CpuSystemMetric.Text = "System  —"; GameCpuMetric.Text = "Gra  —"; CpuTempMetric.Text = "n/d"; GpuTempMetric.Text = "n/d";
        PingMetric.Text = "—";
    }

    private async Task StopSessionAsync()
    {
        _timer.Stop();
        while (_sampleBusy) await Task.Delay(40);
        MonitorButton.Content = "▶  Rozpocznij";
        StatusText.Text = "●  TWORZĘ PODSUMOWANIE";
        _frameRateCapture?.Stop();
        if (_frameRateCapture is not null) _frames.AddRange(_frameRateCapture.ReadNewSamples());
        _captureError = _frameRateCapture?.Error ?? _captureError;
        try { _hardwareSensors?.Dispose(); } catch { }
        _hardwareSensors = null;
        UpdateFrameMetrics();
        SummaryText.Text = BuildSummary();
        SetResultsVisibility(true, false);
        SummaryPanel.Visibility = Visibility.Visible;
        SummaryHeading.Text = $"WYNIK SESJI  ·  {DateTime.Now:HH:mm}";
        StatusText.Text = "●  POMIAR ZAKOŃCZONY";
        StatusText.Foreground = (Brush)FindResource("Accent");
        _frameRateCapture?.Dispose();
        _frameRateCapture = null;
    }

    private void SetResultsVisibility(bool showResults, bool measuring)
    {
        ResultsPanel.Visibility = showResults ? Visibility.Visible : Visibility.Collapsed;
        ChartPanel.Visibility = showResults ? Visibility.Visible : Visibility.Collapsed;
        AwaitingResultsPanel.Visibility = showResults ? Visibility.Collapsed : Visibility.Visible;
        if (measuring)
        {
            AwaitingEyebrow.Text = "POMIAR W TOKU";
            AwaitingTitle.Text = "Zbieram dane z gry";
            AwaitingSubtitle.Text = "Graj normalnie i odtwórz spadki FPS. Po kliknięciu „Zakończ pomiar” pokażę wyniki i ocenę sesji.";
        }
        else
        {
            AwaitingEyebrow.Text = "GOTOWE DO ANALIZY";
            AwaitingTitle.Text = "Wyniki pojawią się po pomiarze";
            AwaitingSubtitle.Text = "Wybierz grę, kliknij rozpocznij i graj chwilę. Po zakończeniu Pulse pokaże pełne podsumowanie.";
        }
    }

    private void ToggleChartSize(object sender, RoutedEventArgs e)
    {
        if (!_chartExpanded)
        {
            _sessionPanelVisibility = SessionPanel.Visibility;
            _resultsPanelVisibility = ResultsPanel.Visibility;
            _summaryPanelVisibility = SummaryPanel.Visibility;
            _awaitingResultsPanelVisibility = AwaitingResultsPanel.Visibility;
            SessionPanel.Visibility = Visibility.Collapsed;
            ResultsPanel.Visibility = Visibility.Collapsed;
            SummaryPanel.Visibility = Visibility.Collapsed;
            AwaitingResultsPanel.Visibility = Visibility.Collapsed;
            Grid.SetRow(ChartPanel, 0);
            Grid.SetRowSpan(ChartPanel, 4);
            ChartPanel.Height = Math.Max(500, MonitorPage.ViewportHeight - 24);
            ChartExpandButton.Content = "⛶  Zmniejsz";
            _chartExpanded = true;
        }
        else
        {
            Grid.SetRow(ChartPanel, 2);
            Grid.SetRowSpan(ChartPanel, 1);
            ChartPanel.Height = double.NaN;
            SessionPanel.Visibility = _sessionPanelVisibility;
            ResultsPanel.Visibility = _resultsPanelVisibility;
            SummaryPanel.Visibility = _summaryPanelVisibility;
            AwaitingResultsPanel.Visibility = _awaitingResultsPanelVisibility;
            ChartExpandButton.Content = "⛶  Powiększ";
            _chartExpanded = false;
        }
        MonitorContent.UpdateLayout();
        DrawChart();
    }

    private void UpdateFrameMetrics()
    {
        if (_frames.Count == 0)
        {
            FpsMetric.Text = "—"; LowFpsMetric.Text = "—"; GpuBusyMetric.Text = "—";
            _captureError ??= _frameRateCapture?.Error;
            FpsSubMetric.ToolTip = _captureError;
            PingMetric.ToolTip = _captureError;
            FpsSubMetric.Text = _captureError
                ?? "czekam na klatki z gry…";
            return;
        }
        var ordered = PrimaryFrames().OrderBy(x => x.TimestampMs).ToArray();
        if (ordered.Length == 0) return;
        var lastTime = ordered[^1].TimestampMs;
        var recent = ordered.Where(x => x.TimestampMs >= lastTime - 1000).ToArray();
        var currentFps = recent.Length == 0 ? 0 : 1000d / recent.Average(x => x.FrameTimeMs);
        var average = 1000d / ordered.Average(x => x.FrameTimeMs);
        FpsMetric.Text = $"{currentFps:0}";
        var slowCount = Math.Max(1, (int)Math.Ceiling(ordered.Length * .01));
        LowFpsMetric.Text = ordered.Length < 100 ? "…" : $"{1000d / ordered.Select(x => x.FrameTimeMs).OrderByDescending(x => x).Take(slowCount).Average():0}";
        FpsSubMetric.Text = $"średnia sesji {average:0} FPS";
        var gpuRows = ordered.Where(x => x.GpuBusyMs is not null).ToArray();
        var gpuPct = gpuRows.Length == 0 ? (double?)null : gpuRows.Average(x => Math.Clamp(x.GpuBusyMs!.Value / x.FrameTimeMs * 100, 0, 100));
        _lastGpuBusyPercent = gpuPct;
        GpuBusyMetric.Text = gpuPct is null ? "n/d" : $"{gpuPct:0}%";
    }

    private string BuildSummary()
    {
        var duration = DateTime.Now - _sessionStarted;
        var lines = new List<string> { $"Gra: {_selectedGame?.Title ?? "brak"}  ·  czas: {duration:mm\\:ss}" };
        var measuredFrames = PrimaryFrames();
        var averageFps = measuredFrames.Count == 0 ? 0 : 1000d / measuredFrames.Average(x => x.FrameTimeMs);
        if (measuredFrames.Count == 0)
        {
            lines.Add("FPS: brak danych. Gra mogła nie renderować obrazu w trakcie sesji albo system zablokował przechwytywanie klatek.");
            if (!string.IsNullOrWhiteSpace(_captureError)) lines.Add("Licznik FPS: " + _captureError);
        }
        else
        {
            var sorted = measuredFrames.OrderBy(x => x.TimestampMs).ToArray();
            lines.Add($"FPS: średnio {averageFps:0}. Zapisano {sorted.Length:N0} klatek z głównego obrazu gry.");
            if (sorted.Length >= 100)
            {
                var lowCount = Math.Max(1, (int)Math.Ceiling(sorted.Length * .01));
                var low = 1000d / sorted.Select(x => x.FrameTimeMs).OrderByDescending(x => x).Take(lowCount).Average();
                lines.Add($"1% low: {low:0} FPS — wynik dla najwolniejszego 1% klatek.");
            }
            else lines.Add("1% low: próbka była za krótka (mniej niż 100 klatek).");
        }
        if (_gameCpuSamples.Count > 0) lines.Add($"CPU gry: średnio {_gameCpuSamples.Average():0}% całkowitej mocy procesora.");
        if (_systemCpuSamples.Count > 0) lines.Add($"CPU systemu: średnio {_systemCpuSamples.Average():0}%.");
        if (_lastGpuBusyPercent is double gpuBusy) lines.Add($"Aktywność GPU na klatkach: około {gpuBusy:0}% czasu klatki.");
        if (_cpuTemperatureSamples.Count > 0) lines.Add($"Temperatura CPU: maks. {_cpuTemperatureSamples.Max():0}°C.");
        else lines.Add("Temperatura CPU: brak sensora dostępnego dla tego sprzętu.");
        if (_gpuTemperatureSamples.Count > 0) lines.Add($"Temperatura GPU: maks. {_gpuTemperatureSamples.Max():0}°C.");
        else lines.Add("Temperatura GPU: brak sensora dostępnego dla tego sprzętu.");
        if (_ramSamples.Count > 0) lines.Add($"RAM: maksymalne zajęcie {_ramSamples.Max():0}%.");
        if (_pingSamples.Count > 0) lines.Add($"Ping do {PingHost.Text.Trim()}: średnio {_pingSamples.Average():0} ms, maks. {_pingSamples.Max()} ms.");

        var observations = new List<string>();
        if (measuredFrames.Count > 30)
        {
            if (averageFps < 30) observations.Add("Średni FPS był niski dla większości gier.");
            if (measuredFrames.Count >= 100)
            {
                var low = 1000d / measuredFrames.Select(x => x.FrameTimeMs).OrderByDescending(x => x).Take(Math.Max(1, (int)Math.Ceiling(measuredFrames.Count * .01))).Average();
                if (low < averageFps * .65) observations.Add("1% low mocno odbiega od średniej — w pomiarze pojawiały się nierówne klatki.");
                if (averageFps >= 50 && low >= averageFps * .75) observations.Add("FPS były dość równe w trakcie tej sesji.");
            }
        }
        if (_cpuTemperatureSamples.Count > 0 && _cpuTemperatureSamples.Max() >= 90) observations.Add("CPU osiągnął co najmniej 90°C. Sprawdź limit temperatury konkretnego modelu i chłodzenie.");
        if (_gpuTemperatureSamples.Count > 0 && _gpuTemperatureSamples.Max() >= 85) observations.Add("GPU osiągnęło co najmniej 85°C. Sprawdź specyfikację karty i przepływ powietrza.");
        if (_ramSamples.Count > 0 && _ramSamples.Max() >= 90) observations.Add("RAM był prawie pełny — zamknij zbędne programy i sprawdź, czy przycięcia się zmniejszą.");
        if (observations.Count == 0) observations.Add("W zebranych danych nie widać wyraźnego sygnału problemu. Wynik dotyczy tylko tej gry i tej sesji.");
        lines.Add("");
        lines.Add("OCENA POMIARU");
        lines.AddRange(observations.Select(x => "•  " + x));
        lines.Add("To wskazówki, nie pewna diagnoza. FPS zależy też od gry, ustawień i odświeżania monitora.");
        return string.Join("\n", lines);
    }

    private void StartFrameRateCapture()
    {
        _captureError = null;
        FpsSubMetric.ToolTip = null;
        _frameRateCapture?.Dispose();
        _frameRateCapture = new FrameRateCapture(_selectedGame!.ProcessId);
        _captureError = _frameRateCapture.Error;
        FpsSubMetric.Text = _captureError ?? "uruchamiam licznik klatek…";
        FpsSubMetric.ToolTip = _captureError;
        PingMetric.ToolTip = _captureError;
    }

    private List<FrameSample> PrimaryFrames()
    {
        if (_frames.Count == 0) return [];
        var mainSwapChain = _frames.GroupBy(x => x.SwapChain).OrderByDescending(group => group.Count()).First().Key;
        return _frames.Where(x => x.SwapChain == mainSwapChain).ToList();
    }

    private async Task SamplePingAsync(string host)
    {
        try
        {
            using var ping = new Ping();
            var result = await ping.SendPingAsync(host, 1200);
            if (result.Status == IPStatus.Success)
            {
                PingMetric.Text = $"{result.RoundtripTime} ms";
                _pingSamples.Add(result.RoundtripTime);
            }
            else PingMetric.Text = "brak odpowiedzi";
        }
        catch { PingMetric.Text = "błąd hosta"; }
    }

    private static void AddHistory(Queue<double> values, double value)
    {
        values.Enqueue(value);
        while (values.Count > 60) values.Dequeue();
    }

    private void DrawChart()
    {
        if (ChartCanvas.ActualWidth < 20 || ChartCanvas.ActualHeight < 20) return;
        ChartCanvas.Children.Clear();
        var w = ChartCanvas.ActualWidth; var h = ChartCanvas.ActualHeight;
        const double left = 42, right = 8, top = 14, bottom = 25;
        var plotH = Math.Max(1, h - top - bottom);
        foreach (var pct in new[] { 0, 50, 100 })
        {
            var y = top + plotH * (100 - pct) / 100;
            var line = new System.Windows.Shapes.Line { X1 = left, X2 = w - right, Y1 = y, Y2 = y, Stroke = (Brush)FindResource("Edge"), StrokeThickness = 1 };
            ChartCanvas.Children.Add(line);
            var label = new TextBlock { Text = $"{pct}%", Foreground = (Brush)FindResource("Muted"), FontSize = 9 };
            Canvas.SetLeft(label, 0); Canvas.SetTop(label, y - 7); ChartCanvas.Children.Add(label);
        }
        Plot(_cpuHistory, (Brush)FindResource("Accent"), left, w - right, top, plotH);
        Plot(_ramHistory, new SolidColorBrush(Color.FromRgb(72, 184, 255)), left, w - right, top, plotH);
        var sampleCount = Math.Max(_cpuHistory.Count, _ramHistory.Count);
        var oldestSeconds = Math.Max(0, sampleCount - 1) * _timer.Interval.TotalSeconds;
        var oldest = new TextBlock { Text = $"{oldestSeconds:0}s temu", Foreground = (Brush)FindResource("Muted"), FontSize = 9 };
        Canvas.SetLeft(oldest, left); Canvas.SetTop(oldest, h - 17); ChartCanvas.Children.Add(oldest);
        var newest = new TextBlock { Text = "teraz", Foreground = (Brush)FindResource("Muted"), FontSize = 9 };
        Canvas.SetLeft(newest, w - right - 28); Canvas.SetTop(newest, h - 17); ChartCanvas.Children.Add(newest);
    }

    private void Plot(IEnumerable<double> source, Brush brush, double left, double right, double top, double height)
    {
        var values = source.ToArray();
        if (values.Length < 2) return;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < values.Length; i++)
            {
                var x = left + (right - left) * i / (values.Length - 1d);
                var y = top + height * (100 - Math.Clamp(values[i], 0, 100)) / 100;
                if (i == 0) ctx.BeginFigure(new Point(x, y), false, false);
                else ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geometry.Freeze();
        ChartCanvas.Children.Add(new System.Windows.Shapes.Path { Data = geometry, Stroke = brush, StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round });
    }

    private async void TestPing(object sender, RoutedEventArgs e)
    {
        var host = PingHost.Text.Trim();
        if (string.IsNullOrWhiteSpace(host)) { PingMetric.Text = "Wpisz host"; return; }
        PingMetric.Text = "…";
        try
        {
            using var ping = new Ping();
            var result = await ping.SendPingAsync(host, 1800);
            PingMetric.Text = result.Status == IPStatus.Success ? $"{result.RoundtripTime} ms" : "Brak odpowiedzi";
        }
        catch { PingMetric.Text = "Błąd hosta"; }
    }

    private void ChartSizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();

    private static SystemSample ReadSystemSample()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) throw new InvalidOperationException("Windows nie zwrócił licznika CPU.");
        double? cpu = null;
        if (_staticLast is { } old)
        {
            var di = idle.Value - old.Idle.Value; var dk = kernel.Value - old.Kernel.Value; var du = user.Value - old.User.Value;
            var total = dk + du;
            if (total > 0) cpu = Math.Clamp(100d * (total - di) / total, 0, 100);
        }
        _staticLast = (idle, kernel, user);
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory)) throw new InvalidOperationException("Windows nie zwrócił licznika pamięci.");
        return new SystemSample(cpu, memory.MemoryLoad,
            (memory.TotalPhysical - memory.AvailablePhysical) / 1024d / 1024 / 1024,
            memory.TotalPhysical / 1024d / 1024 / 1024);
    }

    private static (FileTime Idle, FileTime Kernel, FileTime User)? _staticLast;

    private double? ReadProcessCpu(int pid)
    {
        const uint queryLimitedInformation = 0x1000;
        var handle = OpenProcess(queryLimitedInformation, false, (uint)pid);
        if (handle == IntPtr.Zero) { _processSamples.Remove(pid); return null; }
        try
        {
            if (!GetProcessTimes(handle, out _, out _, out var kernel, out var user)) return null;
            var now = kernel.Value + user.Value;
            var stamp = DateTime.UtcNow;
            if (!_processSamples.TryGetValue(pid, out var prev)) { _processSamples[pid] = (now, stamp); return null; }
            _processSamples[pid] = (now, stamp);
            var elapsed = (stamp - prev.Stamp).TotalSeconds;
            var cpus = Math.Max(1, Environment.ProcessorCount);
            return elapsed <= 0 ? 0 : Math.Clamp((now - prev.Time) / 10_000_000d / elapsed / cpus * 100, 0, 100);
        }
        finally { CloseHandle(handle); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low; public uint High; public readonly ulong Value => ((ulong)High << 32) | Low; }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    private sealed record HardwareInfo(string System, string Cpu, string Ram, double RamGb, double CpuCores, string CpuModel, string[] GpuModels, string[] Gpus, string[] Disks, string Advice);
    private sealed record SystemSample(double? Cpu, uint RamPercent, double RamUsed, double RamTotal);
    private sealed record TelemetrySample(SystemSample System, double? GameCpu, SensorReading? Sensors, List<FrameSample> Frames);
    private readonly record struct ProcessKey(int ProcessId, long StartTimeTicks);
    private sealed record ProcessUsageSample(ProcessKey Key, string Name, bool HasVisibleWindow, long WorkingSetBytes, TimeSpan CpuTime);
    private sealed record GeneralAnalysisSample(DateTime CapturedAt, SystemSample System, List<ProcessUsageSample> Processes);

    private sealed class ProcessUsageAccumulator(ProcessKey key, string name, bool hasVisibleWindow)
    {
        private double _workingSetTotalMb;
        private double _cpuTotal;

        public ProcessKey Key { get; } = key;
        public string Name { get; } = name;
        public bool HasVisibleWindow { get; private set; } = hasVisibleWindow;
        public int MemorySampleCount { get; private set; }
        public int CpuSampleCount { get; private set; }
        public double PeakWorkingSetMb { get; private set; }
        public double PeakCpu { get; private set; }
        public double AverageWorkingSetMb => MemorySampleCount == 0 ? 0 : _workingSetTotalMb / MemorySampleCount;
        public double AverageCpu => CpuSampleCount == 0 ? 0 : _cpuTotal / CpuSampleCount;

        public void AddMemory(long workingSetBytes, bool hasVisibleWindow)
        {
            var megabytes = workingSetBytes / 1024d / 1024;
            HasVisibleWindow |= hasVisibleWindow;
            _workingSetTotalMb += megabytes;
            PeakWorkingSetMb = Math.Max(PeakWorkingSetMb, megabytes);
            MemorySampleCount++;
        }

        public void AddCpu(double cpuPercent)
        {
            _cpuTotal += cpuPercent;
            PeakCpu = Math.Max(PeakCpu, cpuPercent);
            CpuSampleCount++;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetProcessTimes(IntPtr handle, out FileTime creation, out FileTime exit, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
}
