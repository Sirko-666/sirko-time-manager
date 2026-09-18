using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TimerApp.Services;

namespace TimerApp.Windows;

public partial class AppPickerWindow : Window
{
    private bool _initializing;

    public string? PickedPath { get; private set; }

    public AppPickerWindow()
    {
        InitializeComponent();
        RebuildList();
        _initializing = false;
        Loaded += (_, _) => SearchBox.Focus();
    }

    private void RebuildList()
    {
        List.Items.Clear();

        string query = SearchBox.Text.Trim();

        List<InstalledApp> apps = string.IsNullOrEmpty(query)
            ? InstalledAppsService.GetInstalledApps()
            : InstalledAppsService.GetInstalledApps()
                .Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        bool empty = apps.Count == 0;
        EmptyHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        List.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

        foreach (InstalledApp app in apps)
        {
            var text = new TextBlock
            {
                Text = app.Name,
                Foreground = (Brush)FindResource("InkBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            List.Items.Add(new ListBoxItem
            {
                Content = text,
                Tag = app.ShortcutPath,
                Padding = new Thickness(6, 5, 6, 5)
            });
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RebuildList();

    private void OnRowSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (List.SelectedItem is not ListBoxItem { Tag: string path }) return;
        PickedPath = path;
        DialogResult = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
