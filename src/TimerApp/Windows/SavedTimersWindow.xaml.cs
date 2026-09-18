using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TimerApp.Services;

namespace TimerApp.Windows;

public partial class SavedTimersWindow : Window
{
    private readonly List<TimeSpan> _items;
    private readonly List<Row> _rows = [];
    private bool _selectMode;

    public TimeSpan? Picked { get; private set; }

    private sealed class Row
    {
        public required TimeSpan Value { get; init; }
        public required CheckBox Check { get; init; }
        public required Button Delete { get; init; }
    }

    public SavedTimersWindow(List<TimeSpan> items)
    {
        InitializeComponent();
        _items = items;
        BuildList();
        UpdateFooter();
    }

    private void BuildList()
    {
        List.Items.Clear();
        _rows.Clear();

        foreach (TimeSpan value in _items)
            _rows.Add(BuildRow(value));

        bool empty = _items.Count == 0;
        EmptyHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        List.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private Row BuildRow(TimeSpan value)
    {
        var check = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Visibility = _selectMode ? Visibility.Visible : Visibility.Collapsed
        };

        var text = new TextBlock
        {
            Text = Format(value),
            Foreground = (Brush)FindResource("InkBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var delete = new Button
        {
            Style = (Style)FindResource("CrossButtonStyle"),
            Visibility = _selectMode ? Visibility.Collapsed : Visibility.Visible
        };
        delete.Click += (_, e) =>
        {
            _items.Remove(value);
            BuildList();
            e.Handled = true;
        };

        var grid = new Grid { Margin = new Thickness(2, 4, 2, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(check, 0);
        Grid.SetColumn(text, 1);
        Grid.SetColumn(delete, 2);
        grid.Children.Add(check);
        grid.Children.Add(text);
        grid.Children.Add(delete);

        var row = new Row { Value = value, Check = check, Delete = delete };
        grid.MouseLeftButtonDown += (_, e) => OnRowActivated(row, e);

        List.Items.Add(grid);
        return row;
    }

    private void OnRowActivated(Row row, MouseButtonEventArgs e)
    {
        if (_selectMode)
        {
            row.Check.IsChecked = row.Check.IsChecked != true;
            e.Handled = true;
            return;
        }

        Picked = row.Value;
        DialogResult = true;
    }

    // -------- Select mode --------

    private void ToggleSelectMode()
    {
        _selectMode = !_selectMode;
        foreach (Row row in _rows)
        {
            row.Check.Visibility = _selectMode ? Visibility.Visible : Visibility.Collapsed;
            row.Delete.Visibility = _selectMode ? Visibility.Collapsed : Visibility.Visible;
            if (!_selectMode) row.Check.IsChecked = false;
        }
        UpdateFooter();
    }

    private void OnSelectAll()
    {
        foreach (Row row in _rows)
            row.Check.IsChecked = true;
    }

    private void OnDeleteSelected()
    {
        var selected = _rows.Where(r => r.Check.IsChecked == true).Select(r => r.Value).ToList();
        if (selected.Count == 0) return;

        foreach (TimeSpan value in selected)
            _items.Remove(value);

        _selectMode = false;
        BuildList();
        UpdateFooter();
    }

    private void UpdateFooter()
    {
        FooterLeft.Children.Clear();
        FooterRight.Children.Clear();

        if (!_selectMode)
        {
            FooterLeft.Children.Add(MakeGhost("L_Select", ToggleSelectMode));
            return;
        }

        FooterLeft.Children.Add(MakeGhost("L_SelectAll", OnSelectAll));
        var delete = MakeGhost("L_DeleteSelected", OnDeleteSelected);
        delete.Margin = new Thickness(8, 0, 0, 0);
        FooterLeft.Children.Add(delete);

        var done = new Button
        {
            Content = LocalizationService.Get("L_Done"),
            Style = (Style)FindResource("PrimaryButtonStyle")
        };
        done.Click += (_, _) => ToggleSelectMode();
        FooterRight.Children.Add(done);
    }

    private Button MakeGhost(string key, Action onClick)
    {
        var b = new Button
        {
            Content = LocalizationService.Get(key),
            Style = (Style)FindResource("GhostButtonStyle")
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = false;

    internal static string Format(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}";
}