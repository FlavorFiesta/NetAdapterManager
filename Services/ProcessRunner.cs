using System.Diagnostics;

namespace NetAdapterManager.Services;

internal sealed class ProcessTerminationException(string message, Exception inner) : Exception(message, inner);

internal static class ProcessRunner
{
    internal static async Task<(int ExitCode, string Output)> RunAsync(ProcessStartInfo startInfo,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException(L.T("Loc_6D779B4E21E3"));
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(timeout);
        // Drain both pipes concurrently, including while stopping the child.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(lifetime.Token);
            await Task.WhenAll(output, error).WaitAsync(lifetime.Token);
            return (process.ExitCode, string.Join(Environment.NewLine, new[] { output.Result.Trim(), error.Result.Trim() }
                .Where(x => x.Length > 0)));
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                throw new ProcessTerminationException(L.T("Loc_604827F11691"), ex);
            }
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);
            throw new TimeoutException(L.F("Loc_8F590BC3455A", timeout.TotalSeconds));
        }
    }
}
