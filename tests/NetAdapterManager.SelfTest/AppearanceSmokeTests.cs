using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;
using NetAdapterManager.Views;
using Localizer = NetAdapterManager.Services.Localization;

internal static class AppearanceSmokeTests
{
    internal static int Run()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new NetAdapterManager.App(suppressStartup: true); app.InitializeComponent();
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                Localizer.SetLanguage("zh-CN");
                var snapshot = new AdapterSnapshot { Name = "Ethernet (demo)", InterfaceGuid = "demo-guid", Enabled = true,
                    Ipv4InterfacePresent = true, ConnectionStatus = "Up", Type = AdapterType.Physical,
                    Config = NetworkConfig.ForPreset("192.0.2.10", 24, "192.0.2.1", DnsMode.Static, ["192.0.2.53"]) };
                var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([snapshot]),
                    (_, _, _) => throw new Exception("Unexpected network write"), (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
                var vm = new MainViewModel(network);
                vm.Adapters.Add(new AdapterViewModel(snapshot)); vm.SelectedAdapter = vm.Adapters[0];
                vm.PresetGroups.Add(GroupIds.All); vm.PresetGroups.Add(GroupIds.Ungrouped); vm.PresetGroups.Add("Office");
                var preset = new Preset { Name = "Office preset", GroupName = GroupIds.Ungrouped, Config = snapshot.Config.Clone() };
                typeof(MainViewModel).GetField("_allPresets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(vm, new List<Preset> { preset });
                vm.Presets.Add(preset);
                vm.IPv4Address = "192.0.2.99";
                var main = new MainWindow(vm, false); main.Show();
                vm.RefreshCommand.Execute(null); Flush(main);
                Require(vm.IPv4Address == "192.0.2.99" && vm.HasDraft, "A bound adapter refresh lost its draft.");
                var editor = new PresetEditorWindow("Draft preset", GroupIds.Ungrouped, snapshot.Config, "Ethernet (demo)", "00:11:22:33:44:55", [GroupIds.Ungrouped, "Office"]);
                editor.Show();
                using var updateClient = Version161Tests.ReleaseClient();
                var settings = new SettingsWindow(vm, PreferencesService.Default, new UpdateService(updateClient)); settings.Show();
                ((Button)settings.FindName("CheckUpdateButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(settings);
                Require(((Button)settings.FindName("DownloadButton")).IsEnabled, "Simulated stable update did not enable release link.");
                var diagnostics = new DiagnosticsWindow([new OperationRecord { AdapterName = "PRIVATE-DEMO", InterfaceGuid = "private-guid", MacAddress = "AA-BB-CC-DD-EE-FF",
                    Message = "192.0.2.123", ErrorDetail = "Demo failure 192.0.2.123", Before = snapshot.Config.Clone(), Target = snapshot.Config.Clone(), RolledBack = true, Verified = true }]); diagnostics.Show();
                Require(!((TextBox)diagnostics.FindName("PreviewBox")).Text.Contains("PRIVATE-DEMO") && !((TextBox)diagnostics.FindName("PreviewBox")).Text.Contains("192.0.2."), "Default diagnostic preview leaked demo identifiers.");
                var group = new PresetGroupWindow(); group.Show();
                var preview = new ConfigurationPreviewWindow(snapshot, NetworkConfig.ForPreset("192.0.2.99", 24, null, DnsMode.Static, [])); preview.Show();
                var import = new PresetImportWindow([new("Office preset", true), new("New preset", false)]); import.Show();
                var binding = new AdapterBindingWindow(preset, vm.Adapters); binding.Show();
                var output = Path.Combine(Environment.CurrentDirectory, "test-results", "appearance-" + typeof(MainWindow).Assembly.GetName().Version!.ToString(3)); Directory.CreateDirectory(output);
                foreach (var language in new[] { "en", "zh-CN", "zh-TW" })
                foreach (var theme in new[] { "light", "dark" })
                {
                    Localizer.SetLanguage(language); ThemeService.Apply(theme);
                    main.Width = 1000; main.Height = 800;
                    Flush(main); Flush(editor); Flush(settings); Flush(group);
                    Flush(preview); Flush(import); Flush(binding);
                    Flush(diagnostics);
                    Require(((TextBlock)settings.FindName("UpdateStatus")).Text == Localizer.T("Updates.Available"), "Update status did not translate.");
                    Require(((ScrollViewer)main.Content).ScrollableWidth < 2, "Normal main window unexpectedly overflows horizontally.");
                    Require(((ScrollViewer)preview.Content).ScrollableWidth < 2 && ((ScrollViewer)import.Content).ScrollableWidth < 2,
                        "Normal dialog unexpectedly overflows horizontally.");
                    var changes = (DataGrid)preview.FindName("ChangesGrid");
                    Require(changes.Columns[1].ActualWidth > 200 && changes.Columns[2].ActualWidth > 200,
                        "Preview value columns are too narrow.");
                    Require(Equals(changes.Columns[1].Header, Localizer.T("Preview.Current")), "Preview header did not translate.");
                    Require(((SolidColorBrush)preview.Background).Color == ((SolidColorBrush)app.Resources["WindowBackground"]).Color,
                        "Preview background does not follow theme.");
                    Require(Localizer.Language == language, "The requested language was overridden during rendering.");
                    Require(vm.IPv4Address == "192.0.2.99" && ((TextBox)editor.FindName("NameBox")).Text == "Draft preset", "Switching reset a draft.");
                    Require((string)((ComboBox)editor.FindName("GroupBox")).SelectedItem == GroupIds.Ungrouped, "Switching changed a group identity.");
                    Require(main.Title == Localizer.T("Loc_EC57B32F8731"), "Window title did not update.");
                    Require(vm.SelectedAdapter.EnabledText.StartsWith(Localizer.T("Loc_DFB802238B38"), StringComparison.Ordinal), "Adapter text did not update.");
                    Require(Descendants(main).OfType<Button>().Any(b => Equals(b.Content, Localizer.T("Settings.Title"))), "Settings button missing.");
                    Require(ThemeService.EffectiveTheme == theme, "Wrong theme.");
                    foreach (var button in Descendants(main).OfType<Button>().Where(x => x.IsVisible && x.Content is string))
                    {
                        var text = new FormattedText((string)button.Content, System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight, new Typeface(button.FontFamily, button.FontStyle, button.FontWeight, button.FontStretch),
                            button.FontSize, Brushes.Black, 1);
                        Require(text.WidthIncludingTrailingWhitespace <= button.ActualWidth - button.Padding.Left - button.Padding.Right + 2,
                            $"Button label clipped after language switch: {button.Content}");
                    }
                    Require(Contrast((SolidColorBrush)app.Resources["TextColor"], (SolidColorBrush)app.Resources["CardBackground"]) >= 4.5, "Insufficient body text contrast.");
                    Require(Contrast((SolidColorBrush)app.Resources["MutedText"], (SolidColorBrush)app.Resources["CardBackground"]) >= 4.5, "Insufficient secondary text contrast.");
                    Save(main, Path.Combine(output, $"main-{language}-{theme}.png"));
                    Save(editor, Path.Combine(output, $"editor-{language}-{theme}.png"));
                    Save(settings, Path.Combine(output, $"settings-{language}-{theme}.png"));
                    Save(preview, Path.Combine(output, $"preview-{language}-{theme}.png"));
                    Save(import, Path.Combine(output, $"import-{language}-{theme}.png"));
                    Save(binding, Path.Combine(output, $"binding-{language}-{theme}.png"));
                    Save(diagnostics, Path.Combine(output, $"diagnostics-{language}-{theme}.png"));
                }
                var savedAdapter = new AdapterViewModel(new AdapterSnapshot { Name = "Offline adapter (demo)",
                    InterfaceGuid = "offline-demo-guid", Enabled = false, UsesSavedIPv4Configuration = true,
                    Config = snapshot.Config.Clone() });
                vm.Adapters.Add(savedAdapter); vm.SelectedAdapter = savedAdapter;
                foreach (var language in new[] { "en", "zh-CN", "zh-TW" })
                foreach (var theme in new[] { "light", "dark" })
                {
                    Localizer.SetLanguage(language); ThemeService.Apply(theme); Flush(main);
                    var status = (TextBlock)main.FindName("AdapterConfigurationStatus");
                    Require(status.Text == Localizer.T("Network.ShowingSaved") && status.ActualHeight > 0,
                        "Saved configuration source label did not render or translate.");
                    Require(vm.IPv4Address == "192.0.2.10", "Offline saved IP missing from editor.");
                    Save(main, Path.Combine(output, $"saved-{language}-{theme}.png"));
                }
                editor.Close(); settings.Close(); group.Close(); preview.Close(); import.Close(); binding.Close(); diagnostics.Close();
                var preferencePath = Path.Combine(output, "isolated-settings-" + Guid.NewGuid().ToString("N") + ".json");
                var preferenceService = new PreferencesService(preferencePath);
                preferenceService.Load();
                var interactiveSettings = new SettingsWindow(vm, preferenceService); interactiveSettings.Show();
                var languageBox = (ComboBox)interactiveSettings.FindName("LanguageBox");
                languageBox.SelectedItem = languageBox.Items.Cast<ComboBoxItem>().Single(x => (string)x.Tag == "en");
                WaitSaved(interactiveSettings);
                var themeBox = (ComboBox)interactiveSettings.FindName("ThemeBox");
                themeBox.SelectedItem = themeBox.Items.Cast<ComboBoxItem>().Single(x => (string)x.Tag == "dark");
                WaitSaved(interactiveSettings);
                var reloaded = new PreferencesService(preferencePath); reloaded.Load();
                Require(reloaded.Current.Language == "en" && reloaded.Current.Theme == "dark", "Settings controls did not persist the choice.");
                Require(((SolidColorBrush)interactiveSettings.Background).Color == ((SolidColorBrush)app.Resources["WindowBackground"]).Color, "Settings window background does not follow theme.");
                interactiveSettings.Close();
                Localizer.SetLanguage("en"); ThemeService.Apply("light");
                var overview = new PresetOverviewWindow(vm); overview.Show(); Flush(overview);
                Save(overview, Path.Combine(output, "overview-en-light.png")); overview.Close();
                // Exercise busy settings without starting a real operation.
                var completion = new TaskCompletionSource();
                var busy = vm.RunBusyAsync(() => completion.Task);
                var busySettings = new SettingsWindow(vm); busySettings.Show(); Flush(busySettings);
                Require(!((ComboBox)busySettings.FindName("LanguageBox")).IsEnabled && ((ComboBox)busySettings.FindName("ThemeBox")).IsEnabled,
                    "Busy operation should block only language changes.");
                completion.SetResult(); Flush(busySettings); busySettings.Close();
                Localizer.SetLanguage("en");
                main.Width = 800; main.Height = 600;
                foreach (var scale in new[] { 1.25, 1.5, 2.0 })
                {
                    ((FrameworkElement)main.Content).LayoutTransform = new ScaleTransform(scale, scale);
                    Flush(main);
                    Require(Descendants(main).OfType<ScrollViewer>().Any(s => s.ScrollableWidth > 0), "Small scaled viewport needs horizontal scrolling.");
                    Save(main, Path.Combine(output, $"layout-scale-{scale:F2}.png"));
                }
                ((FrameworkElement)main.Content).LayoutTransform = Transform.Identity;
                main.Width = 640; main.Height = 420; Flush(main);
                Require(main.InputBindings.Count >= 3, "Keyboard shortcuts missing.");
                Save(main, Path.Combine(output, "minimum-en.png"));
                ThemeService.ApplyForEnvironment("dark", true); Flush(main);
                Require(ThemeService.EffectiveTheme == "high-contrast" && ((SolidColorBrush)app.Resources["TextColor"]).Color == SystemColors.WindowTextColor, "High contrast palette not applied.");
                Save(main, Path.Combine(output, "high-contrast-palette-en.png"));
                ThemeService.Apply("dark"); Require(ThemeService.EffectiveTheme == "dark", "Theme preference not restored.");
                main.Close(); app.Shutdown();
                Console.WriteLine("Appearance smoke: 6 language/theme combinations; 54 screenshots including diagnostics privacy, simulated update UI, preview/import/binding, saved state, simulated 125/150/200% layout scaling, minimum size and injected high-contrast palette. Real DPI/monitor transitions and English Windows not exercised. No real HTTP or network writes.");
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error is null) return 0;
        Console.Error.WriteLine(error); return 1;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void WaitSaved(SettingsWindow window)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        do { Flush(window); } while (window.IsSaving && DateTime.UtcNow < deadline);
        Require(!window.IsSaving && ((TextBlock)window.FindName("ErrorText")).Text.Length == 0, "Settings save failed or timed out.");
    }
    private static void Flush(Window window)
    {
        window.UpdateLayout();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame); window.UpdateLayout();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Save(Window window, string path)
    {
        var visual = (FrameworkElement)window.Content;
        var width = visual.ActualWidth + visual.Margin.Left + visual.Margin.Right;
        var height = visual.ActualHeight + visual.Margin.Top + visual.Margin.Bottom;
        var canvas = new DrawingVisual();
        using (var drawing = canvas.RenderOpen())
        {
            drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            drawing.DrawRectangle(new VisualBrush(visual), null, new Rect(visual.Margin.Left, visual.Margin.Top, visual.ActualWidth, visual.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static double Contrast(SolidColorBrush first, SolidColorBrush second)
    {
        static double Channel(byte value) { var x = value / 255d; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
        static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        var a = Luminance(first.Color); var b = Luminance(second.Color);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
}
