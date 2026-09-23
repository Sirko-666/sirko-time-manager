using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using TimerApp.Services;

namespace TimerApp.Windows;

public partial class SoundPickerWindow : Window
{
    private bool _initializing = true;

    public string? Result { get; private set; }

    public SoundPickerWindow(string currentKey)
    {
        InitializeComponent();
        BuildList(currentKey);
        VolumeSlider.Value = SoundService.Volume;
        SoundService.VolumeChanged += OnSoundVolumeChanged;
        _initializing = false;
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing) return;
        SoundService.Volume = e.NewValue;
    }

    private void OnSoundVolumeChanged(double value)
    {
        if (Math.Abs(VolumeSlider.Value - value) < 0.0005) return;
        _initializing = true;
        VolumeSlider.Value = value;
        _initializing = false;
    }

    private void BuildList(string selectKey)
    {
        List.Items.Clear();

        var all = SoundService.All();
        var user = all.Where(k => k.StartsWith("user:", StringComparison.OrdinalIgnoreCase)).ToList();
        var standard = all.Where(k => !k.StartsWith("user:", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (string key in user)
            List.Items.Add(MakeItem(key));

        if (user.Count > 0 && standard.Count > 0)
            List.Items.Add(MakeSeparator());

        foreach (string key in standard)
            List.Items.Add(MakeItem(key));

        SelectCurrent(selectKey);
    }

    private ListBoxItem MakeItem(string key)
    {
        bool isUser = key.StartsWith("user:", StringComparison.OrdinalIgnoreCase);
        var text = new TextBlock
        {
            Text = SoundService.DisplayName(key),
            Foreground = (Brush)FindResourceHelper("InkBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        var item = new ListBoxItem
        {
            Tag = key,
            Padding = new Thickness(8, 5, 8, 5),
            Margin = isUser ? new Thickness(0, 3, 0, 9) : new Thickness(0, 0, 0, 0)
        };

        if (key.StartsWith("user:", StringComparison.OrdinalIgnoreCase))
        {
            var delete = new Button
            {
                Style = (Style)FindResourceHelper("CrossButtonStyle"),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = LocalizationService.Get("L_Delete")
            };
            delete.Click += (_, e) =>
            {
                e.Handled = true;
                DeleteUserSound(key);
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(text, 0);
            Grid.SetColumn(delete, 1);
            grid.Children.Add(text);
            grid.Children.Add(delete);

            item.Content = grid;
        }
        else
        {
            item.Content = text;
        }

        return item;
    }

    private void DeleteUserSound(string key)
    {
        SoundService.Stop();
        if (!SoundService.DeleteUserSound(key)) return;

        // Rebuild silently: don't auto-preview whatever gets selected next.
        _initializing = true;
        string currentKey = List.SelectedItem is ListBoxItem { Tag: string current } ? current : key;
        BuildList(currentKey);
        _initializing = false;
    }

    private static ListBoxItem MakeSeparator()
    {
        var line = new Border
        {
            Height = 1,
            Background = (Brush)FindResourceHelper("HairlineBrush"),
            Margin = new Thickness(6, 6, 6, 6)
        };
        return new ListBoxItem
        {
            Content = line,
            Focusable = false,
            IsHitTestVisible = false,
            IsEnabled = false
        };
    }

    private static object FindResourceHelper(string key) =>
        Application.Current.FindResource(key);

    private void SelectCurrent(string key)
    {
        ListBoxItem? match = null;
        foreach (object entry in List.Items)
        {
            if (entry is ListBoxItem item && (string?)item.Tag == key)
            {
                match = item;
                break;
            }
        }

        match ??= List.Items.Count > 0 ? List.Items[0] as ListBoxItem : null;
        if (match is null) return;

        List.SelectedItem = match;
        List.ScrollIntoView(match);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (List.SelectedItem is ListBoxItem { Tag: string key })
            SoundService.Play(key);
    }

    private void OnAddSoundClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Audio|*.wav;*.mp3|WAV|*.wav|MP3|*.mp3",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        string? key = SoundService.AddUserSound(dialog.FileName);
        if (key is null) return;

        BuildList(key);
        SoundService.Play(key);
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not ListBoxItem { Tag: string key }) return;
        Result = key;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    protected override void OnClosed(EventArgs e)
    {
        SoundService.VolumeChanged -= OnSoundVolumeChanged;
        SoundService.Stop();
        base.OnClosed(e);
    }
}
