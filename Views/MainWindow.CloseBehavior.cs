using System.ComponentModel;
using System.Windows;
using NetAdapterManager.Services;
using Forms = System.Windows.Forms;

namespace NetAdapterManager.Views;

public partial class MainWindow
{
    private bool _handlingClose;
    private bool _allowClose;
    private bool _closed;
    private Forms.NotifyIcon? _trayIcon;
    private System.Drawing.Icon? _trayImage;
    private Forms.ContextMenuStrip? _trayMenu;

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _allowClose) return;

        e.Cancel = true;
        RequestClose(forceExit: false);
    }

    private void RequestClose(bool forceExit)
    {
        if (_handlingClose || _closed) return;
        _handlingClose = true;
        // Leave the original Closing event before opening a dialog or closing again.
        Dispatcher.BeginInvoke(new Action(() => _ = HandleCloseAsync(forceExit)));
    }

    private async Task HandleCloseAsync(bool forceExit)
    {
        try
        {
            if (_closed) return;
            if (forceExit) RestoreFromTray();
            var behavior = forceExit ? CloseBehavior.Exit : _preferences.Current.CloseBehavior;
            if (behavior == CloseBehavior.Ask)
            {
                var dialog = new CloseChoiceWindow { Owner = this };
                if (dialog.ShowDialog() != true) return;
                behavior = dialog.Choice;
                if (dialog.Remember)
                {
                    IsEnabled = false;
                    try { await _preferences.SaveAsync(_preferences.Current with { CloseBehavior = behavior }); }
                    catch (Exception ex)
                    {
                        AppLogger.Error("Could not save close behavior", ex);
                        if (!_closed)
                            MessageBox.Show(this, L.T("Settings.SaveError") + " " + ex.Message,
                                L.T("Tray.CloseBehavior"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            if (_closed) return;
            if (behavior == CloseBehavior.MinimizeToTray)
            {
                HideInTray();
                return;
            }

            if (_viewModel.HasPendingOperation)
            {
                IsEnabled = false;
                _viewModel.ShowToast(L.T("Loc_CB3734A5E3A1"), L.T("Loc_30C8E670B34D"), false);
                await _viewModel.ActiveOperation;
            }
            if (_closed) return;
            _allowClose = true;
            Close();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not close or hide the main window", ex);
            if (!_closed) _viewModel.ReportUnexpectedError(ex);
        }
        finally
        {
            _handlingClose = false;
            _allowClose = false;
            if (!_closed) IsEnabled = true;
        }
    }

    private void HideInTray()
    {
        if (_trayIcon is null)
        {
            try
            {
                using var stream = Application.GetResourceStream(
                    new Uri("pack://application:,,,/NetAdapterManager;component/Assets/AppIcon.ico")).Stream;
                using var image = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
                _trayImage = (System.Drawing.Icon)image.Clone();
                _trayMenu = new Forms.ContextMenuStrip();
                _trayMenu.Items.Add("", null, (_, _) => RestoreFromTray());
                _trayMenu.Items.Add("", null, (_, _) => RequestClose(forceExit: true));
                _trayIcon = new Forms.NotifyIcon { Icon = _trayImage, ContextMenuStrip = _trayMenu };
                _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
            }
            catch
            {
                DisposeTray();
                throw;
            }
        }
        // Refresh labels if the language changed since last showing the icon.
        _trayIcon.Text = L.T("Loc_EC57B32F8731");
        _trayMenu!.Items[0].Text = L.T("Tray.Restore");
        _trayMenu.Items[1].Text = L.T("Tray.Exit");
        _trayIcon.Visible = true;
        Hide();
    }

    private void RestoreFromTray()
    {
        if (_closed) return;
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        if (_trayIcon is not null) _trayIcon.Visible = false;
    }

    private void DisposeTray()
    {
        _trayIcon?.Dispose();
        _trayMenu?.Dispose();
        _trayImage?.Dispose();
        _trayIcon = null;
        _trayMenu = null;
        _trayImage = null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        DisposeTray();
        base.OnClosed(e);
    }
}
