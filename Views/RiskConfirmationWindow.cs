using System.Windows;
using System.Windows.Controls;
using NetAdapterManager.Services;
namespace NetAdapterManager.Views;
internal sealed class RiskConfirmationWindow : Window
{
    internal RiskConfirmationWindow(string title, string message, Window? owner)
    {
        Title = title; Width = 540; Height = 320; MinWidth = 420; MinHeight = 240;
        if (owner?.IsVisible == true) Owner = owner;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "WindowBackground"); SetResourceReference(ForegroundProperty, "TextColor");
        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        grid.Children.Add(new ScrollViewer { Content = text, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = L.T("Ops.Cancel"), IsCancel = true, IsDefault = true };
        var confirm = new Button { Content = L.T("Ops.Confirm") };
        confirm.Click += (_, _) => DialogResult = true; cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(cancel); buttons.Children.Add(confirm); Grid.SetRow(buttons, 1); grid.Children.Add(buttons);
        Content = grid; WindowCompatibility.Attach(this);
    }
    internal static bool Confirm(Window? owner, string message, string title) => new RiskConfirmationWindow(title, message, owner).ShowDialog() == true;
}
