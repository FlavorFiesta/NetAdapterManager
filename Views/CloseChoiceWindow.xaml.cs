using System.Windows;
using NetAdapterManager.Services;

namespace NetAdapterManager.Views;

internal partial class CloseChoiceWindow : Window
{
    internal CloseBehavior Choice { get; private set; }
    internal bool Remember => RememberChoiceBox.IsChecked == true;

    public CloseChoiceWindow()
    {
        InitializeComponent();
        UiDisplayService.Attach(this);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseBehavior.Exit;
        DialogResult = true;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseBehavior.MinimizeToTray;
        DialogResult = true;
    }
}
