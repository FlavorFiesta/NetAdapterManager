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
    private readonly UpdateService? _updates;
    private CancellationTokenSource? _updateCancellation;
    private UpdateResult _updateResult = new("NotChecked");
    internal bool IsSaving => _saving;
    public SettingsWindow(MainViewModel viewModel) : this(viewModel, PreferencesService.Default) { }
    internal SettingsWindow(MainViewModel viewModel, PreferencesService preferences, UpdateService? updates = null)
    {
        _preferences = preferences;
        _updates = updates;
        InitializeComponent();
        NetAdapterManager.Services.WindowCompatibility.Attach(this);
        DataContext = viewModel;
        RestoreSelection();
        RestoreDisplay();
        FallbackUrlBox.Text = _preferences.Current.UpdateFallbackUrl;
        if (_preferences.LoadError is { } error) ErrorText.Text = L.T("Settings.LoadError") + " " + error;
        _loading = false;
        UpdateDisplayPreview();
        RefreshUpdateText();
        System.ComponentModel.PropertyChangedEventManager.AddHandler(L.Instance, (_, _) => RefreshUpdateText(), string.Empty);
    }
    private void RestoreSelection()
    {
        var prefs = _preferences.Current;
        LanguageBox.SelectedItem = LanguageBox.Items.Cast<ComboBoxItem>().First(x => (string)x.Tag == prefs.Language);
        ThemeBox.SelectedItem = ThemeBox.Items.Cast<ComboBoxItem>().First(x => (string)x.Tag == prefs.Theme);
        CloseBehaviorBox.SelectedItem = CloseBehaviorBox.Items.Cast<ComboBoxItem>()
            .First(item => (string)item.Tag == prefs.CloseBehavior.ToString());
    }
    private async void Preference_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _saving || LanguageBox.SelectedItem is not ComboBoxItem language
            || ThemeBox.SelectedItem is not ComboBoxItem theme
            || CloseBehaviorBox.SelectedItem is not ComboBoxItem closeBehavior) return;
        _saving = true;
        IsEnabled = false;
        try
        {
            var previous = _preferences.Current;
            var next = previous with
            {
                Language = (string)language.Tag,
                Theme = (string)theme.Tag,
                CloseBehavior = Enum.Parse<CloseBehavior>((string)closeBehavior.Tag)
            };
            await _preferences.SaveAsync(next);
            if (next.Language != previous.Language || next.Theme != previous.Theme)
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
    { if (_saving) e.Cancel = true; base.OnClosing(e); if (!e.Cancel) _updateCancellation?.Cancel(); }
    private void RefreshUpdateText()
    {
        VersionStatus.Text = L.F("Updates.Versions", typeof(SettingsWindow).Assembly.GetName().Version!.ToString(3), _updateResult.LatestVersion ?? L.T("Updates.Unknown"));
        UpdateStatus.Text = L.T("Updates." + _updateResult.Status);
        if (_updateResult.UsedFallback) UpdateStatus.Text += " " + L.T("Updates.UsedFallback");
        ReleaseSummary.Text = _updateResult.Summary ?? "";
    }
    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updateCancellation is not null) return;
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        CheckUpdateButton.IsEnabled = false; DownloadButton.IsEnabled = false;
        _updateResult = new("Checking"); RefreshUpdateText();
        try
        {
            _updateResult = await (_updates ?? new UpdateService(fallbackUrl: _preferences.Current.UpdateFallbackUrl))
                .CheckAsync(typeof(SettingsWindow).Assembly.GetName().Version!, cancellation.Token);
            RefreshUpdateText();
            DownloadButton.IsEnabled = _updateResult.Url is not null && UpdateService.TrustedUrl(_updateResult.Url);
        }
        finally { _updateCancellation = null; CheckUpdateButton.IsEnabled = true; }
    }
    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (!UpdateService.TrustedUrl(_updateResult.Url)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_updateResult.Url!) { UseShellExecute = true }); }
        catch (Exception) { _updateResult = _updateResult with { Status = "OpenFailed" }; RefreshUpdateText(); }
    }
    private async void SaveFallback_Click(object sender, RoutedEventArgs e)
    {
        if (_saving || _updateCancellation is not null) return;
        var value = FallbackUrlBox.Text.Trim();
        if (value.Length > 0 && !UpdateService.ValidFeedUrl(value))
        { ErrorText.Text = L.T("Updates.InvalidFeed"); return; }
        _saving = true; IsEnabled = false;
        try
        {
            await _preferences.SaveAsync(_preferences.Current with { UpdateFallbackUrl = value });
            FallbackUrlBox.Text = _preferences.Current.UpdateFallbackUrl;
            ErrorText.Text = L.T("Updates.FeedSaved");
        }
        catch (Exception ex) { ErrorText.Text = L.T("Settings.SaveError") + " " + ex.Message; }
        finally { _saving = false; IsEnabled = true; }
    }
    private void RestoreDisplay()
    {
        var p = _preferences.Current;
        FollowDpiBox.IsChecked = p.FollowSystemDpi;
        UiScaleSlider.Value = p.UiScale * 100;
        FontScaleSlider.Value = p.FontScale * 100;
    }
    private void Display_Changed(object sender, RoutedEventArgs e) { if (!_loading) UpdateDisplayPreview(); }
    private void UpdateDisplayPreview()
    {
        if (UiScaleSlider is null || FontScaleSlider is null || DisplayPreview is null) return;
        UiScaleSlider.IsEnabled = FollowDpiBox.IsChecked != true;
        var scale = FollowDpiBox.IsChecked == true ? 1 : UiScaleSlider.Value / 100;
        DisplayPreview.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
        DisplayPreviewText.FontSize = 13 * FontScaleSlider.Value / 100;
        PreviewAddressText.FontSize = 12 * FontScaleSlider.Value / 100;
    }
    private void DisplayPreset_Click(object sender, RoutedEventArgs e)
    {
        var (scale, font) = ((sender as Button)?.Tag as string) switch
        { "small" => (85d, 90d), "large" => (115d, 110d), "xl" => (130d, 120d), _ => (100d, 100d) };
        FollowDpiBox.IsChecked = (sender as Button)?.Tag as string == "default"; UiScaleSlider.Value = scale; FontScaleSlider.Value = font; UpdateDisplayPreview();
    }
    private void ResetDisplay_Click(object sender, RoutedEventArgs e)
    { FollowDpiBox.IsChecked = true; UiScaleSlider.Value = FontScaleSlider.Value = 100; UpdateDisplayPreview(); ApplyDisplay_Click(sender, e); }
    private async void ApplyDisplay_Click(object sender, RoutedEventArgs e)
    {
        if (_saving) return;
        _saving = true;
        try
        {
            await _preferences.SaveAsync(_preferences.Current with { FollowSystemDpi = FollowDpiBox.IsChecked == true, UiScale = UiScaleSlider.Value / 100, FontScale = FontScaleSlider.Value / 100 });
            UiDisplayService.Apply(_preferences.Current);
            ErrorText.Text = "";
        }
        catch (Exception ex) { ErrorText.Text = L.T("Settings.SaveError") + " " + ex.Message; }
        finally { _saving = false; }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
