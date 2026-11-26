using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Pulse;

public static class UiLanguage
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["Pulse | diagnostyka PC"] = "Pulse | PC diagnostics",
        ["DIAGNOSTYKA WYDAJNOŚCI"] = "PERFORMANCE DIAGNOSTICS",
        ["UWAGA  •  WERSJA BETA 0.1.8"] = "NOTE  •  BETA VERSION 0.1.8",
        ["Aplikacja jest w fazie testów."] = "This application is in testing.",
        ["Uruchom aplikację"] = "Launch application",
        ["Uruchamiam…"] = "Starting…",
        ["PANEL DIAGNOSTYCZNY DLA WINDOWS"] = "DIAGNOSTICS DASHBOARD FOR WINDOWS",
        ["DIAGNOSTYKA"] = "DIAGNOSTICS",
        ["▦  Przegląd sprzętu"] = "▦  Hardware overview",
        ["◉  Analiza ogólna"] = "◉  General analysis",
        ["ANALIZA GRY"] = "GAME ANALYSIS",
        ["◫  Pomiar i wyniki"] = "◫  Monitoring and results",
        ["TRYB PRYWATNY"] = "PRIVACY",
        ["Dane są odczytywane lokalnie z komputera."] = "Data is read locally from this computer.",
        ["Wersja Beta 0.1.8"] = "Beta version 0.1.8",
        ["Diagnostyka wydajności"] = "Performance diagnostics",
        ["Zbierz dane, zanim zdecydujesz o wymianie części."] = "Collect data before deciding to replace hardware.",
        ["Sprawdź aktualizacje"] = "Check for updates",
        ["●  GOTOWY"] = "●  READY",
        ["Przegląd sprzętu"] = "Hardware overview",
        ["Najważniejsze informacje o komputerze w jednym miejscu."] = "Key information about your computer in one place.",
        ["⟳   Skanuj komputer"] = "⟳   Scan computer",
        ["Kliknij „Skanuj komputer”, aby wykryć podzespoły."] = "Click “Scan computer” to detect hardware.",
        ["PROCESOR"] = "PROCESSOR",
        ["PAMIĘĆ RAM"] = "MEMORY",
        ["KARTY GRAFICZNE"] = "GRAPHICS CARDS",
        ["DYSKI"] = "DRIVES",
        ["DOBRANIE CPU I GPU"] = "CPU AND GPU BALANCE",
        ["Uruchom skan, aby oszacować dobranie procesora do karty graficznej."] = "Scan to estimate the balance between the processor and graphics card.",
        ["JAK INTERPRETOWAĆ WYNIKI"] = "HOW TO INTERPRET RESULTS",
        ["Podzespoły same nie przesądzają o przyczynie spadków FPS. Zbierz pomiary podczas gry i sprawdź temperatury."] = "Hardware alone cannot explain FPS drops. Measure while playing and check temperatures.",
        ["Analiza ogólna"] = "General analysis",
        ["Obserwuj zasoby komputera podczas zwykłego użytkowania."] = "Monitor computer resources during everyday use.",
        ["CZAS OBSERWACJI"] = "MONITORING DURATION",
        ["Zbieranie danych odbywa się lokalnie i nie zamyka żadnych programów."] = "Data is collected locally; no programs are closed.",
        ["Czas (min)"] = "Duration (min)",
        ["Wpisz czas od 1 do 120 minut"] = "Enter a duration from 1 to 120 minutes",
        ["▶  Rozpocznij analizę"] = "▶  Start analysis",
        ["Zakończ i pokaż raport"] = "Finish and show report",
        ["Gotowe do rozpoczęcia. Domyślny czas analizy to 10 minut."] = "Ready to start. The default analysis duration is 10 minutes.",
        ["RAPORT ZASOBÓW"] = "RESOURCE REPORT",
        ["Po analizie pokażę średnie obciążenie CPU i RAM oraz procesy, które w czasie obserwacji zużywały najwięcej pamięci lub procesora."] = "The report shows average CPU and memory usage and the processes that used the most resources.",
        ["SESJA POMIAROWA"] = "MONITORING SESSION",
        ["Przygotuj analizę gry"] = "Set up game analysis",
        ["GOTOWE DO STARTU"] = "READY TO START",
        ["Uruchom grę, odtwórz spadek FPS i zostaw Pulse otwarte w tle."] = "Start the game, reproduce the FPS drop, and leave Pulse running in the background.",
        ["Nie wybrano gry"] = "No game selected",
        ["Wybierz aktywne okno gry, aby mierzyć użycie jej procesu."] = "Select an active game window to measure its process usage.",
        ["Wybierz grę"] = "Choose game",
        ["Odśwież listę okien"] = "Refresh window list",
        ["▶  Rozpocznij"] = "▶  Start",
        ["PING"] = "PING",
        ["Adres IP lub nazwa serwera do testu ping"] = "IP address or server name to ping",
        ["Testuj host"] = "Ping host",
        ["Adres serwera gry lub host do sprawdzenia"] = "Game server address or host to check",
        ["Pomiar FPS odczytuje zdarzenia klatek wybranej gry; Windows może poprosić o zgodę administratora. Temperatury zależą od dostępnych czujników."] = "FPS monitoring reads frame events from the selected game; Windows may request administrator permission. Temperature readings depend on available sensors.",
        ["TELEMETRIA NA ŻYWO"] = "LIVE TELEMETRY",
        ["Najważniejsze wyniki sesji"] = "Key session results",
        ["ODŚWIEŻANIE CO 2 S"] = "REFRESH EVERY 2 S",
        ["FPS ŚREDNIE"] = "AVERAGE FPS",
        ["czekam na grę"] = "waiting for game",
        ["płynność najwolniejszych klatek"] = "smoothness of the slowest frames",
        ["CPU • OBCIĄŻENIE"] = "CPU • LOAD",
        ["System  —"] = "System  —",
        ["Gra  —"] = "Game  —",
        ["GPU • ZAJĘTOŚĆ KLATEK"] = "GPU • FRAME BUSY",
        ["czas GPU w klatce gry"] = "GPU time per game frame",
        ["CPU • TEMPERATURA"] = "CPU • TEMPERATURE",
        ["GPU • TEMPERATURA"] = "GPU • TEMPERATURE",
        ["PING HOSTA"] = "HOST PING",
        ["Obciążenie w czasie"] = "Usage over time",
        ["Ostatnie 60 pomiarów • około 2 minut"] = "Last 60 samples • about 2 minutes",
        ["CPU systemu"] = "System CPU",
        ["⛶  Powiększ"] = "⛶  Expand",
        ["GOTOWE DO ANALIZY"] = "READY TO ANALYZE",
        ["Wyniki pojawią się po pomiarze"] = "Results will appear after monitoring",
        ["Wybierz grę, kliknij rozpocznij i graj chwilę. Po zakończeniu Pulse pokaże pełne podsumowanie."] = "Choose a game, start monitoring, and play for a while. Pulse will show a summary when you finish.",
        ["Wskaż okno gry."] = "Select the game window.",
        ["Zbierz dane"] = "Collect data",
        ["Odtwórz problem z FPS."] = "Reproduce the FPS issue.",
        ["Sprawdź wynik"] = "Review results",
        ["Oceń raport sesji."] = "Review the session report.",
        ["PODSUMOWANIE SESJI"] = "SESSION SUMMARY",
        ["Kopiuj raport"] = "Copy report",
        ["Zapisz jako TXT"] = "Save as TXT",
        ["KATEGORIE • WKRÓTCE"] = "MORE CATEGORIES • COMING SOON",
        ["FPS i stabilność • Wkrótce"] = "FPS and stability • Coming soon",
        ["Bateria i energia • Wkrótce"] = "Battery and power • Coming soon",
        ["Sieć i opóźnienia • Wkrótce"] = "Network and latency • Coming soon",
        ["Język / Language"] = "Language",
        ["Język aplikacji"] = "Application language",
        ["Ustawienia języka"] = "Language settings",
        ["Ustawienia gry"] = "Game settings",
        ["Sprawdź połączenie i spróbuj ponownie."] = "Check your connection and try again.",
        ["Pobieranie aktualizacji…"] = "Downloading update…",
        ["Nie udało się pobrać aktualizacji."] = "Could not download the update.",
        ["Aktualizacja gotowa. Uruchamiam ponownie…"] = "Update ready. Restarting…",
        ["Wersja beta"] = "Beta version",
        ["Zapisano raport: "] = "Report saved: ",
        ["Raport skopiowano do schowka."] = "Report copied to clipboard.",
        ["Błąd"] = "Error",
        ["Zapisz raport Pulse"] = "Save Pulse report",
        ["Raport tekstowy (*.txt)|*.txt"] = "Text report (*.txt)|*.txt",
        ["WYBÓR PROCESU"] = "PROCESS SELECTION",
        ["Którą grę monitorujemy?"] = "Which game should be monitored?",
        ["Wykryte aktywne okna"] = "Active windows detected",
        ["Tylko procesy z widocznym oknem są pokazywane."] = "Only processes with a visible window are shown.",
        ["Anuluj"] = "Cancel",
        ["Monitoruj tę grę"] = "Monitor this game",
        ["Szukaj po nazwie gry lub procesu"] = "Search by game or process name",
        ["Brak pasujących okien — zmień frazę wyszukiwania."] = "No matching windows — change your search.",
        ["aktywnych okien • wybierz proces gry do pomiaru"] = "active windows • select a game process to monitor",
        ["Aktualizacja Pulse"] = "Pulse update",
        ["Nowa wersja jest gotowa do pobrania"] = "A new version is ready to download",
        ["LISTA ZMIAN"] = "RELEASE NOTES",
        ["Później"] = "Later",
        ["DOSTĘPNA WERSJA"] = "AVAILABLE VERSION",
        ["Lista zmian"] = "What's new",
        ["Pobierz i uruchom ponownie"] = "Download and restart",
        ["Pobieranie"] = "Downloading",
        ["Brak opisu zmian dla tej wersji."] = "No release notes are available for this version.",
        ["Ponów"] = "Retry",
        ["Zamknij"] = "Close"
    };

    public static string Get(string polish, string language)
        => language == "en" && English.TryGetValue(polish, out var translated) ? translated : polish;

    public static void Apply(Window window, string language)
    {
        window.Title = Translate(window.Title, language);

        foreach (var element in Descendants(window))
        {
            if (element is TextBlock textBlock && !BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty))
                textBlock.Text = Translate(textBlock.Text, language);
            else if (element is Button button && button.Content is string content)
                button.Content = Translate(content, language);

            if (element is FrameworkElement frameworkElement && frameworkElement.ToolTip is string toolTip)
                frameworkElement.ToolTip = Translate(toolTip, language);
        }
    }

    private static string Translate(string value, string language)
    {
        if (language == "en")
            return English.TryGetValue(value, out var translated) ? translated : value;

        return English.FirstOrDefault(x => x.Value == value).Key ?? value;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
