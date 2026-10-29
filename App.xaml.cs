using System.Windows;
using System.Windows.Threading;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

namespace NetAdapterManager;

public partial class App : Application
{
    private SingleInstanceLease? _instance;
    private readonly bool _suppressStartup;

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (_suppressStartup) return;
        PreferencesService.Default.Load();
        PreferencesService.Default.Apply();
        ThemeService.Start();
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException(L.T("Loc_4B9AC26F8B2F"));
        try { _instance = new SingleInstanceLease(@"Global\NetAdapterManager-" + user); }
        catch (Exception ex)
        {
            AppLogger.Error("无法建立单实例保护", ex);
            MessageBox.Show(L.T("Loc_19A5E080978D"), L.T("Loc_0CF642CF998C"));
            Shutdown();
            return;
        }
        if (!_instance.Acquired)
        {
            MessageBox.Show(L.T("Loc_65E25A5E51DE"), L.T("Loc_D04AE84E1D93"));
            Shutdown();
            return;
        }
        // Persist the first-run installation language once; upgrades must not overwrite it.
        if (!System.IO.File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "settings.json")))
        {
            try { await PreferencesService.Default.SaveAsync(PreferencesService.Default.Current); }
            catch (Exception ex) { AppLogger.Error("Could not persist first-run preferences", ex); }
        }
        base.OnStartup(e);
        MainWindow = new Views.MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeService.Stop();
        _instance?.Dispose();
        base.OnExit(e);
    }

    public App() : this(false) { }
    internal App(bool suppressStartup)
    {
        _suppressStartup = suppressStartup;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        AppLogger.Info("应用进程启动");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Unhandled("Dispatcher 未处理异常", e.Exception);
        try
        {
            if (MainWindow?.DataContext is MainViewModel viewModel)
                viewModel.ReportUnexpectedError(e.Exception);
        }
        catch (Exception reportException)
        {
            AppLogger.Unhandled("显示未处理异常提示失败", reportException);
        }
        e.Handled = true;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.Unhandled("Task 未观察异常", e.Exception);
        e.SetObserved();
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            AppLogger.Unhandled("AppDomain 未处理异常", exception);
        else
            AppLogger.Error($"AppDomain 未处理异常：{e.ExceptionObject}");
    }
}
