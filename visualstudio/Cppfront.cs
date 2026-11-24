using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Cpp2.VisualStudio;

/// <summary>What one cppfront run printed, and whether it succeeded.</summary>
public sealed record CppfrontResult(string CommandLine, int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>An error or warning cppfront reported.</summary>
/// <param name="File">The file it names, as a full path.</param>
/// <param name="Line">One-based, or 0 when cppfront gave none.</param>
/// <param name="Column">One-based, or 0 when cppfront gave none.</param>
public sealed record CppfrontDiagnostic(string File, int Line, int Column, bool IsError, string Message)
{
    // cppfront's form, as MSVC's: path(line,col): error: message
    static readonly Regex Pattern = new(
        @"^(?<file>.+?)\((?<line>\d+)(?:,(?<column>\d+))?\): (?<severity>error|warning): (?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The diagnostics in cppfront's output. Relative paths are taken from folder.</summary>
    public static IReadOnlyList<CppfrontDiagnostic> Parse(string output, string folder)
    {
        var diagnostics = new List<CppfrontDiagnostic>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Pattern.Match(line);
            if (!match.Success)
            {
                continue;
            }
            var file = match.Groups["file"].Value;
            diagnostics.Add(new CppfrontDiagnostic(
                Path.IsPathRooted(file) ? file : Path.GetFullPath(Path.Combine(folder, file)),
                int.Parse(match.Groups["line"].Value),
                match.Groups["column"].Success ? int.Parse(match.Groups["column"].Value) : 0,
                match.Groups["severity"].Value == "error",
                match.Groups["message"].Value));
        }
        return diagnostics;
    }
}

/// <summary>Runs cppfront on one file.</summary>
public static class Cppfront
{
    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Transpiles source to output with the given options. cppfront runs in
    /// output's folder, where it leaves an empty cpp2_post_build.sh.
    /// </summary>
    public static async Task<CppfrontResult> RunAsync(
        string cppfront, IReadOnlyList<string> options, string source, string output, CancellationToken cancellationToken)
    {
        var arguments = options.Concat([source, "-output", output]).Select(Quote);
        var startInfo = new ProcessStartInfo(cppfront, string.Join(" ", arguments))
        {
            WorkingDirectory = Path.GetDirectoryName(output),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        var commandLine = $"{Quote(cppfront)} {startInfo.Arguments}";

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

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using (timeout.Token.Register(() => exited.TrySetCanceled()))
        {
            try
            {
                await exited.Task;
            }
            catch (TaskCanceledException)
            {
                process.Kill();
                throw new OperationCanceledException(cancellationToken.IsCancellationRequested
                    ? "Canceled"
                    : $"cppfront didn't finish within {Timeout.TotalSeconds} seconds");
            }
        }
        // Exited can fire before the last redirected lines arrive; this waits for them
        process.WaitForExit();

        lock (printed)
        {
            return new CppfrontResult(commandLine, process.ExitCode, printed.ToString());
        }
    }

    static string Quote(string argument) =>
        argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"']) < 0 ? argument : $"\"{argument.Replace("\"", "\\\"")}\"";
}
