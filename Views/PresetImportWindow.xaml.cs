using System.Windows;
using NetAdapterManager.Services;

namespace NetAdapterManager.Views;
public partial class PresetImportWindow : Window
{
    public ImportConflictPolicy Policy => PolicyBox.SelectedIndex == 1 ? ImportConflictPolicy.Replace : ImportConflictPolicy.Skip;
    public PresetImportWindow(IEnumerable<PresetImportRow> rows) { InitializeComponent();
        NetAdapterManager.Services.WindowCompatibility.Attach(this); RowsList.ItemsSource = rows; }
    private void Import_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
