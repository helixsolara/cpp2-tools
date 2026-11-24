using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cpp2.VisualStudio;

/// <summary>What one run of a tool printed, and whether it succeeded.</summary>
public sealed record ToolResult(string CommandLine, int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Runs a command-line tool without a window and collects what it prints.</summary>
public static class ToolRunner
{
    /// <summary>
    /// Runs fileName with arguments in workingDirectory, and kills it when it
    /// takes longer than timeout or cancellationToken is canceled.
    /// </summary>
    public static async Task<ToolResult> RunAsync(
        string fileName, string arguments, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var commandLine = $"{Quote(fileName)} {arguments}";

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var printed = new StringBuilder();
        void Collect(object sender, DataReceivedEventArgs line)
        {
            if (line.Data != null)
            {
                lock (printed)
                {
                    printed.AppendLine(line.Data);
                }
            }
        }
        process.OutputDataReceived += Collect;
        process.ErrorDataReceived += Collect;

        var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Exited += (_, _) => exited.TrySetResult(true);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        using (limit.Token.Register(() => exited.TrySetCanceled()))
        {
            try
            {
                await exited.Task;
            }
            catch (TaskCanceledException)
            {
                KillTree(process);
                throw new OperationCanceledException(cancellationToken.IsCancellationRequested
                    ? "Canceled"
                    : $"{fileName} didn't finish within {timeout.TotalSeconds} seconds");
            }
        }
        // Exited can fire before the last redirected lines arrive; this waits for them
        process.WaitForExit();

        lock (printed)
        {
            return new ToolResult(commandLine, process.ExitCode, printed.ToString());
        }
    }

    /// <summary>Quotes an argument for a Windows command line when it needs it.</summary>
    public static string Quote(string argument) =>
        argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"']) < 0 ? argument : $"\"{argument.Replace("\"", "\\\"")}\"";

    // A cmd.exe script's children (cl, link) would outlive a plain Kill
    static void KillTree(Process process)
    {
        try
        {
            using var killer = Process.Start(new ProcessStartInfo("taskkill", $"/T /F /PID {process.Id}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            killer?.WaitForExit(5000);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already gone
        }
    }
}
