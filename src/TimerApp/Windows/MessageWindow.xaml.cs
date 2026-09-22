using System.Windows;

namespace TimerApp.Windows;

/// <summary>Simple information popup with a single close button.</summary>
public partial class MessageWindow : Window
{
    public MessageWindow(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
