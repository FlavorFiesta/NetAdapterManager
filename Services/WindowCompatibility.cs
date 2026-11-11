using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace NetAdapterManager.Services;

internal static class WindowCompatibility
{
    internal static void Attach(Window window)
    {
        var minimumWidth = window.MinWidth;
        var minimumHeight = window.MinHeight;
        if (window.Content is FrameworkElement content && content is not ScrollViewer)
        {
            var frame = window.WindowStyle == WindowStyle.None ? 0 : 32;
            window.Content = null;
            var viewport = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var layout = new Grid { MinWidth = Math.Max(0, minimumWidth - frame), MinHeight = Math.Max(0, minimumHeight - frame) };
            layout.SetBinding(FrameworkElement.WidthProperty, new Binding(nameof(ScrollViewer.ViewportWidth)) { Source = viewport });
            layout.SetBinding(FrameworkElement.HeightProperty, new Binding(nameof(ScrollViewer.ViewportHeight)) { Source = viewport });
            layout.Children.Add(content);
            viewport.Content = layout;
            window.Content = viewport;
        }
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            var source = HwndSource.FromHwnd(handle);
            source?.AddHook((IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (message is 0x02E0 or 0x007E) // DPI or monitor topology changed.
                    window.Dispatcher.BeginInvoke(() => Fit(window, handle, minimumWidth, minimumHeight), DispatcherPriority.Loaded);
                return IntPtr.Zero;
            });
            window.Dispatcher.BeginInvoke(() => Fit(window, handle, minimumWidth, minimumHeight), DispatcherPriority.Loaded);
        };
    }
    private static void Fit(Window window, IntPtr handle, double minimumWidth, double minimumHeight)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref info) || !GetWindowRect(handle, out var rect)) return;
        var area = info.Work;
        var dpi = Math.Max(96, GetDpiForWindow(handle)) / 96d;
        var maxWidth = (area.Right - area.Left) / dpi;
        var maxHeight = (area.Bottom - area.Top) / dpi;
        window.MinWidth = Math.Min(minimumWidth, maxWidth);
        window.MinHeight = Math.Min(minimumHeight, maxHeight);
        window.MaxWidth = maxWidth; window.MaxHeight = maxHeight;
        var width = Math.Min(rect.Right - rect.Left, area.Right - area.Left);
        var height = Math.Min(rect.Bottom - rect.Top, area.Bottom - area.Top);
        SetWindowPos(handle, IntPtr.Zero, Math.Clamp(rect.Left, area.Left, area.Right - width),
            Math.Clamp(rect.Top, area.Top, area.Bottom - height), width, height, 0x0014); // no Z-order/focus changes
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
