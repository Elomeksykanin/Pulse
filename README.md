# Pulse

Pulse to natywna aplikacja diagnostyczna dla Windows, napisana w C# i WPF (.NET 8). Interfejs jest zbudowany w XAML; nie wymaga Pythona.

## Uruchomienie

Wymagany jest .NET 8 Desktop Runtime. Z katalogu projektu uruchom:

```powershell
dotnet run --project .\Pulse\Pulse.csproj
```

Kompilacja wersji Release:

```powershell
dotnet build .\Pulse\Pulse.csproj -c Release
```

## Instalowanie i aktualizacje

Instalator oraz aktualizacje są publikowane jako GitHub Releases. Pulse sprawdza aktualizacje przy uruchomieniu, a po zgodzie pobiera je i uruchamia ponownie aplikację. Automatyczne aktualizacje działają dla wersji zainstalowanej instalatorem Velopack; uruchomienie skopiowanego pliku EXE nie wystarcza.

Nowe wydanie tworzy się przez workflow **Release** na karcie **Actions**, podając wersję w formacie `X.Y.Z` (np. `0.2.0`), albo przez wypchnięcie tagu `vX.Y.Z`. Workflow buduje instalator i publikuje pliki aktualizacji. Przed kolejnym wydaniem zwiększ wersję w `Pulse/Pulse.csproj` i zaktualizuj `CHANGELOG.md`.

Okno aktualizacji pokazuje informacje o wydaniu i postęp pobierania. Jeśli pobieranie się nie powiedzie, można ponowić próbę bez zamykania aplikacji.

Raport analizy ogólnej można skopiować do schowka lub zapisać jako plik TXT. Pulse zapamiętuje ostatnio wybraną grę i przywraca ją przy następnym uruchomieniu.

## Co mierzy

- Skan sprzętu przez Windows CIM: system, procesor, RAM, karty graficzne, sterowniki i dyski.
- Obciążenie CPU całego systemu z natywnego licznika Windows.
- Obciążenie CPU wybranego procesu gry (procent całkowitej mocy logicznych procesorów).
- Zajętość pamięci fizycznej i wykres ostatnich 60 próbek.
- Ping na żądanie do podanego hosta.
- FPS, 1% low i czas aktywności GPU na klatkach przez PresentMon.
- Temperatury CPU/GPU przez LibreHardwareMonitor, o ile sprzęt udostępnia odpowiednie czujniki.

Przy rozpoczęciu sesji Windows może poprosić o zgodę administratora, ponieważ przechwytywanie klatek używa ETW. Jeżeli zgoda nie zostanie udzielona, pozostałe pomiary nadal działają, ale FPS nie będą dostępne. Czujniki temperatur zależą od sprzętu i czasem mogą nie być udostępniane. 1% low jest wyliczane po zebraniu co najmniej 100 klatek.

Ocena sesji opisuje tylko zebrane dane i nie jest pewną diagnozą ani automatyczną rekomendacją zakupu. FPS zależą między innymi od gry, ustawień i odświeżania monitora.

## Zewnętrzne komponenty

- [PresentMon 2.6.0](https://github.com/GameTechDev/PresentMon), narzędzie do pomiaru czasu klatek. Licencja dołączona w `Pulse/Resources/PresentMon-LICENSE.txt`.
- [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), NuGet 0.9.6 do odczytu czujników.
