using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Cpp2.VisualStudio;

/// <summary>
/// Build > Transpile Cpp2 File (also on the editor's context menu): saves the
/// active .cpp2 or .h2 file, runs cppfront on it as its CMake build would (or
/// with the options page's settings), reports to the Cpp2 output pane and the
/// Error List, and opens the result read-only. The result goes to a temporary
/// folder, so the build's own generated files are left alone.
/// </summary>
public sealed class TranspileCommand
{
    public static readonly Guid CommandSet = new("ee5a82cb-47b1-4c03-a1b1-cc4c6890155e");
    public const int CommandId = 0x0100;

    // The same test as the CMake module's: a module interface unit or partition
    static readonly Regex ModuleInterface = new(@"^[ \t]*(export[ \t]+module[ \t]|module[ \t]+[^;:]+:)", RegexOptions.Multiline);

    readonly AsyncPackage _package;
    readonly Cpp2Options _options;
    readonly Cpp2Output _output;
    readonly DTE2 _dte;

    TranspileCommand(AsyncPackage package, Cpp2Options options, Cpp2Output output, DTE2 dte)
    {
        _package = package;
        _options = options;
        _output = output;
        _dte = dte;
    }

    public static async Task RegisterAsync(AsyncPackage package, Cpp2Options options, Cpp2Output output, CancellationToken cancellationToken)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var commands = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
            ?? throw new InvalidOperationException("The menu command service is missing");
        var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2
            ?? throw new InvalidOperationException("The DTE service is missing");

        var command = new TranspileCommand(package, options, output, dte);
        var menuCommand = new OleMenuCommand(command.Execute, new CommandID(CommandSet, CommandId));
        menuCommand.BeforeQueryStatus += command.UpdateStatus;
        commands.AddCommand(menuCommand);
    }

    // Shows the command only while a Cpp2 file is the active document
    void UpdateStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var command = (OleMenuCommand)sender;
        command.Visible = command.Enabled = IsCpp2(ActiveDocumentPath());
    }

    void Execute(object sender, EventArgs e)
    {
        _package.JoinableTaskFactory.RunAsync(() => TranspileActiveDocumentAsync(_package.DisposalToken))
            .FileAndForget("cpp2/transpile");
    }

    async Task TranspileActiveDocumentAsync(CancellationToken cancellationToken)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var document = _dte.ActiveDocument;
        var source = document?.FullName;
        if (document == null || !IsCpp2(source))
        {
            return;
        }
        if (!document.Saved)
        {
            document.Save();
        }

        _output.Start($"Transpiling {source}");
        await TaskScheduler.Default;
        var plan = Plan(source!);
        var result = await RunAsync(plan, source!, cancellationToken);

        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        Report(plan, result);
    }

    // How to transpile source: as a CMake build that lists it does, else with the options page
    TranspilePlan Plan(string source)
    {
        var build = SourcesFile.Find(source);
        var output = TemporaryOutput(source, build?.BuildOutput);
        if (build != null)
        {
            return new TranspilePlan(build.Cppfront, build.Options, output, "the settings of the CMake build that lists it");
        }
        var cppfront = string.IsNullOrWhiteSpace(_options.CppfrontPath) ? "cppfront.exe" : _options.CppfrontPath.Trim();
        var options = _options.Arguments.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        return new TranspilePlan(cppfront, options, output, "Tools > Options > Cpp2, since no CMake build lists it");
    }

    static async Task<CppfrontResult?> RunAsync(TranspilePlan plan, string source, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(plan.Output)!);
        if (File.Exists(plan.Output))
        {
            File.SetAttributes(plan.Output, FileAttributes.Normal);
        }
        try
        {
            var result = await Cppfront.RunAsync(plan.Cppfront, plan.Options, source, plan.Output, cancellationToken);
            if (result.Succeeded && File.Exists(plan.Output))
            {
                // Read-only, since edits there would be lost on the next run
                File.SetAttributes(plan.Output, FileAttributes.ReadOnly);
            }
            return result;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // cppfront couldn't be started
        }
    }

    void Report(TranspilePlan plan, CppfrontResult? result)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _output.WriteLine($"Using {plan.Origin}.");
        if (result == null)
        {
            _output.WriteLine($"Couldn't start {plan.Cppfront}. Configure the file's CMake build, or set the cppfront path in Tools > Options > Cpp2.");
            _output.Show();
            return;
        }

        _output.WriteLine(result.CommandLine);
        _output.WriteLine(result.Output.TrimEnd());
        _output.Report(CppfrontDiagnostic.Parse(result.Output, Path.GetDirectoryName(plan.Output)!));
        if (!result.Succeeded)
        {
            _output.WriteLine($"cppfront failed with exit code {result.ExitCode}.");
            _output.Show();
            return;
        }
        VsShellUtilities.OpenDocument(_package, plan.Output);
        _dte.StatusBar.Text = $"Transpiled to {plan.Output}";
    }

    // %TEMP%\cpp2-tools\transpiled\<hash of the source path>\<name>.<ext>, the
    // extension being the build's when known, else what the build would choose
    static string TemporaryOutput(string source, string? buildOutput)
    {
        var extension = buildOutput != null ? Path.GetExtension(buildOutput) : GeneratedExtension(source);
        var folder = Path.Combine(Path.GetTempPath(), "cpp2-tools", "transpiled", ShortHash(Path.GetFullPath(source).ToUpperInvariant()));
        return Path.Combine(folder, Path.GetFileNameWithoutExtension(source) + extension);
    }

    static string GeneratedExtension(string source)
    {
        if (string.Equals(Path.GetExtension(source), ".h2", StringComparison.OrdinalIgnoreCase))
        {
            return ".h";
        }
        return ModuleInterface.IsMatch(File.ReadAllText(source)) ? ".ixx" : ".cpp";
    }

    static string ShortHash(string text)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        return string.Concat(hash.Take(6).Select(b => b.ToString("x2")));
    }

    string? ActiveDocumentPath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return _dte.ActiveDocument?.FullName;
        }
        catch (ArgumentException)
        {
            // Some windows, such as the start page, throw rather than give no document
            return null;
        }
    }

    static bool IsCpp2(string? path) =>
        path != null
        && (path.EndsWith(".cpp2", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".h2", StringComparison.OrdinalIgnoreCase));

    sealed record TranspilePlan(string Cppfront, System.Collections.Generic.IReadOnlyList<string> Options, string Output, string Origin);
}
