using System.Windows;
using NetAdapterManager.Models;
using NetAdapterManager.ViewModels;
namespace NetAdapterManager.Views;
public partial class AdapterBindingWindow : Window
{
    public AdapterViewModel? Selected => AdapterBox.SelectedItem as AdapterViewModel;
    public AdapterBindingWindow(Preset preset, IEnumerable<AdapterViewModel> adapters)
    {
        InitializeComponent();
        NetAdapterManager.Services.WindowCompatibility.Attach(this);
        OriginalText.Text = $"{preset.Name}\n{preset.AdapterName}\n{preset.AdapterInterfaceGuid}\n{preset.AdapterMacAddress}";
        AdapterBox.ItemsSource = adapters;
    }
    private void Bind_Click(object sender, RoutedEventArgs e) { if (Selected is not null) DialogResult = true; }
}
