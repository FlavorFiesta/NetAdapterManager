using System.Windows;
using System.Windows.Threading;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

namespace NetAdapterManager;

public partial class App : Application
{
    private SingleInstanceLease? _instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("无法确认当前用户身份。");
        try { _instance = new SingleInstanceLease(@"Global\NetAdapterManager-" + user); }
        catch (Exception ex)
        {
            AppLogger.Error("无法建立单实例保护", ex);
            MessageBox.Show("无法确认是否已有程序运行，本次启动已停止。请关闭其他实例后重试。", "启动失败");
            Shutdown();
            return;
        }
        if (!_instance.Acquired)
        {
            MessageBox.Show("网络适配器管理器已在运行，请使用已打开的窗口。", "程序已运行");
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }

    public App()
    {
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
