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
            MessageBox.Show("请输入分组名称。", "无法添加", MessageBoxButton.OK, MessageBoxImage.Information);
            GroupNameBox.Focus();
            return;
        }
        if (string.Equals(name, "全部分组", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("“全部分组”是系统筛选项，请使用其他名称。", "无法添加", MessageBoxButton.OK, MessageBoxImage.Information);
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
