using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Pulse;

public partial class GamePickerWindow : Window
{
    private readonly ObservableCollection<GameWindowInfo> _games;
    private readonly ICollectionView _view;
    private readonly string _language;
    public GameWindowInfo? SelectedGame { get; private set; }

    public GamePickerWindow(IEnumerable<GameWindowInfo> games, string language)
    {
        InitializeComponent();
        _language = language;
        UiLanguage.Apply(this, _language);
        _games = new ObservableCollection<GameWindowInfo>(games);
        _view = CollectionViewSource.GetDefaultView(_games);
        _view.Filter = FilterGame;
        GameList.ItemsSource = _view;
        UpdateCount();
        SearchBox.ToolTip = UiLanguage.Get("Szukaj po nazwie gry lub procesu", _language);
    }

    private bool FilterGame(object item)
    {
        if (item is not GameWindowInfo game) return false;
        var query = SearchBox?.Text?.Trim();
        return string.IsNullOrEmpty(query) || game.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || game.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || game.ProcessId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void SearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _view?.Refresh();
        UpdateCount();
    }

    private void UpdateCount()
    {
        if (CountLabel is null) return;
        var count = _view?.Cast<object>().Count() ?? 0;
        CountLabel.Text = count == 0
            ? UiLanguage.Get("Brak pasujących okien — zmień frazę wyszukiwania.", _language)
            : $"{count} {UiLanguage.Get("aktywnych okien • wybierz proces gry do pomiaru", _language)}";
    }

    private void GameSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => SelectButton.IsEnabled = GameList.SelectedItem is GameWindowInfo;

    private void AcceptSelection(object sender, MouseButtonEventArgs e) => Accept();
    private void AcceptClick(object sender, RoutedEventArgs e) => Accept();
    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Accept()
    {
        if (GameList.SelectedItem is not GameWindowInfo game) return;
        SelectedGame = game;
        DialogResult = true;
    }
}

public sealed record GameWindowInfo(int ProcessId, string Name, string Title)
{
    public string ProcessLabel => $"PID {ProcessId}";
}
