using System.Windows;
using System.Windows.Threading;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

namespace NetAdapterManager;

public partial class App : Application
{
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
