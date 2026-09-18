using System.Windows;
using System.Windows.Input;

namespace TimerApp.Windows;

public partial class UninstallDoneWindow : Window
{
    public UninstallDoneWindow(string title, string message, string subText)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        SubText.Text = subText;

        PreviewMouseDown += OnAnyInput;
        PreviewKeyDown += OnAnyInput;
        Loaded += (_, _) => Activate();
    }

    private void OnAnyInput(object sender, InputEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
