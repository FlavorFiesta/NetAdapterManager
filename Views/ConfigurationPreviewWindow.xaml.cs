using System.Windows;
using NetAdapterManager.Models;
using NetAdapterManager.Services;

namespace NetAdapterManager.Views;

public partial class ConfigurationPreviewWindow : Window
{
    private readonly AdapterSnapshot _adapter;
    private readonly NetworkConfig _target;
    public ConfigurationPreviewWindow(AdapterSnapshot adapter, NetworkConfig target)
    {
        _adapter = adapter; _target = target.Clone();
        InitializeComponent();
        NetAdapterManager.Services.WindowCompatibility.Attach(this);
        IdentityText.Text = $"{adapter.Name}\n{adapter.InterfaceGuid} | #{adapter.InterfaceIndex} | {adapter.MacAddress}";
        RefreshText();
        System.ComponentModel.PropertyChangedEventManager.AddHandler(L.Instance, (_, _) => RefreshText(), string.Empty);
    }
    private void RefreshText()
    {
        ChangesGrid.Columns[0].Header = L.T("Preview.Field");
        ChangesGrid.Columns[1].Header = L.T("Preview.Current");
        ChangesGrid.Columns[2].Header = L.T("Preview.Target");
        ChangesGrid.ItemsSource = ConfigurationPreview.Create(_adapter, _target);
        WarningsText.Text = string.Join("\n", ValidationService.ValidateManualConfig(_target).Warnings);
    }
    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
