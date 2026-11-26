using System.Windows;
using System.Windows.Input;

namespace Pulse;

public partial class UpdateDialog : Window
{
    public UpdateDialog(string version)
    {
        InitializeComponent();
        VersionText.Text = version;
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnLaterClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            DialogResult = false;
    }
}
