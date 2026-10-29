using System.Windows;
using System.Windows.Controls;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

namespace NetAdapterManager.Views;

public partial class SettingsWindow : Window
{
    private bool _loading = true;
    private bool _saving;
    private readonly PreferencesService _preferences;
    internal bool IsSaving => _saving;
    public SettingsWindow(MainViewModel viewModel) : this(viewModel, PreferencesService.Default) { }
    internal SettingsWindow(MainViewModel viewModel, PreferencesService preferences)
    {
        _preferences = preferences;
        InitializeComponent();
        DataContext = viewModel;
        RestoreSelection();
        if (_preferences.LoadError is { } error) ErrorText.Text = L.T("Settings.LoadError") + " " + error;
        _loading = false;
    }
    private void RestoreSelection()
    {
        var prefs = _preferences.Current;
        LanguageBox.SelectedItem = LanguageBox.Items.Cast<ComboBoxItem>().First(x => (string)x.Tag == prefs.Language);
        ThemeBox.SelectedItem = ThemeBox.Items.Cast<ComboBoxItem>().First(x => (string)x.Tag == prefs.Theme);
    }
    private async void Preference_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _saving || LanguageBox.SelectedItem is not ComboBoxItem language
            || ThemeBox.SelectedItem is not ComboBoxItem theme) return;
        _saving = true;
        IsEnabled = false;
        try
        {
            await _preferences.SaveAsync(new AppPreferences { Language = (string)language.Tag, Theme = (string)theme.Tag });
            _preferences.Apply();
            ErrorText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _loading = true;
            RestoreSelection();
            _loading = false;
            ErrorText.Text = L.T("Settings.SaveError") + " " + ex.Message;
        }
        finally { _saving = false; IsEnabled = true; }
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    { if (_saving) e.Cancel = true; base.OnClosing(e); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
