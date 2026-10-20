using System.IO;
using System.Text;

namespace NetAdapterManager.Services;

/// <summary>
/// Local application log for diagnostics. Logging must never throw back to
/// the UI and turn a handled error into a second crash.
/// </summary>
public static class AppLogger
{
    private static readonly object Gate = new();
    private const long MaxLogBytes = 4 * 1024 * 1024;

    public static string LogDirectory => Path.Combine(AppContext.BaseDirectory, "logs");
    public static string LogFilePath => Path.Combine(LogDirectory, "NetAdapterManager.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string context, Exception? exception = null) =>
        Write("ERROR", context, exception);

    public static void Unhandled(string context, Exception exception) =>
        Write("FATAL", context, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var builder = new StringBuilder()
                .Append('[').Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                .Append("] [").Append(level).Append("] ").AppendLine(message);
            if (exception is not null) builder.AppendLine(exception.ToString());

            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded();
                File.AppendAllText(LogFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // A read-only install directory must not make logging crash the app.
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(LogFilePath) || new FileInfo(LogFilePath).Length < MaxLogBytes) return;
            var oldPath = LogFilePath + ".old";
            File.Delete(oldPath);
            File.Move(LogFilePath, oldPath);
        }
        catch
        {
            // Best effort only; the next append can still succeed.
        }
    }
}
