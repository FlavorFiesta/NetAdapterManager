using System.Windows;
using System.Windows.Input;

namespace NetAdapterManager.Views;

public partial class PresetGroupWindow : Window
{
    public string GroupName { get; private set; } = string.Empty;

    public PresetGroupWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => GroupNameBox.Focus();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = GroupNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(L.T("Loc_38FD97A244EA"), L.T("Loc_42D98D4EFBF1"), MessageBoxButton.OK, MessageBoxImage.Information);
            GroupNameBox.Focus();
            return;
        }
        if (string.Equals(name, NetAdapterManager.Models.GroupIds.All, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(L.T("Loc_976373AD11AE"), L.T("Loc_42D98D4EFBF1"), MessageBoxButton.OK, MessageBoxImage.Information);
            GroupNameBox.Focus();
            return;
        }

        GroupName = name;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
