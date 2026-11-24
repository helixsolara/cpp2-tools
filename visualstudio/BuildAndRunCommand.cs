using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Cpp2.VisualStudio;

/// <summary>
/// Build > Build and Run Cpp2 File (also on the editor's context menu): builds
/// the active .cpp2 file on its own, without CMake or a project, and runs it in
/// a console window. It transpiles with the cppfront and options from Tools >
/// Options > Cpp2 (the bundled cppfront by default), compiles a debug build
/// with this Visual Studio's x64 compiler, and reports errors to the Cpp2
/// output pane and the Error List. Everything it writes goes to a temporary folder.
/// </summary>
public sealed class BuildAndRunCommand
{
    public const int CommandId = 0x0102;

    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    readonly AsyncPackage _package;
    readonly Cpp2Options _options;
    readonly Cpp2Output _output;
    readonly DTE2 _dte;

    BuildAndRunCommand(AsyncPackage package, Cpp2Options options, Cpp2Output output, DTE2 dte)
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

        var command = new BuildAndRunCommand(package, options, output, dte);
        var menuCommand = new OleMenuCommand(command.Execute, new CommandID(TranspileCommand.CommandSet, CommandId));
        menuCommand.BeforeQueryStatus += command.UpdateStatus;
        commands.AddCommand(menuCommand);
    }

    // Shows the command only for .cpp2 files: a .h2 header isn't a program
    void UpdateStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var command = (OleMenuCommand)sender;
        var path = Cpp2Documents.ActivePath(_dte);
        command.Visible = command.Enabled = path != null && path.EndsWith(".cpp2", StringComparison.OrdinalIgnoreCase);
    }

    void Execute(object sender, EventArgs e)
    {
        _package.JoinableTaskFactory.RunAsync(() => BuildAndRunAsync(_package.DisposalToken))
            .FileAndForget("cpp2/buildandrun");
    }

    async Task BuildAndRunAsync(CancellationToken cancellationToken)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var document = _dte.ActiveDocument;
        var source = document?.FullName;
        if (document == null || source == null || !source.EndsWith(".cpp2", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (!document.Saved)
        {
            document.Save();
        }
        var vcVars = VcVarsPath();
        var build = new SingleFileBuild(
            vcVars, _options.Cppfront(), _options.CppfrontOptions(), IncludeDirFor(_options.Cppfront()), source, WorkDir(source));
        _output.Start($"Building {source}");
        _dte.StatusBar.Text = $"Building {Path.GetFileName(source)}...";

        if (!File.Exists(vcVars))
        {
            Fail($"{vcVars} is missing. Building needs Visual Studio's Desktop development with C++ workload.");
            return;
        }

        await TaskScheduler.Default;
        Directory.CreateDirectory(build.WorkDir);
        File.WriteAllText(build.ScriptPath, build.Script());
        var result = await ToolRunner.RunAsync(
            "cmd.exe", $"/d /s /c \"\"{build.ScriptPath}\"\"", build.WorkDir, Timeout, cancellationToken);

        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        _output.WriteLine(result.Output.TrimEnd());
        _output.Report(Diagnostic.Parse(result.Output, build.WorkDir));
        if (!result.Succeeded)
        {
            Fail($"The build failed with exit code {result.ExitCode}. The build script is {build.ScriptPath}.");
            return;
        }
        _output.WriteLine($"Running {build.Program}");
        _dte.StatusBar.Text = $"Running {Path.GetFileName(build.Program)}";
        Run(build.Program, Path.GetDirectoryName(source)!);
    }

    void Fail(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _output.WriteLine(message);
        _output.Show();
        _dte.StatusBar.Text = "Build and Run Cpp2 File failed";
    }

    // In its own console window, which stays open after the program ends
    static void Run(string program, string workingDirectory)
    {
        using var _ = System.Diagnostics.Process.Start(new ProcessStartInfo("cmd.exe", $"/d /s /c \"\"{program}\" & echo. & pause\"")
        {
            UseShellExecute = true,
            WorkingDirectory = workingDirectory,
        });
    }

    // devenv.exe is in <Visual Studio>\Common7\IDE; vcvars64.bat in <Visual Studio>\VC\Auxiliary\Build
    string VcVarsPath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var visualStudio = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_dte.FullName)!, "..", ".."));
        return Path.Combine(visualStudio, "VC", "Auxiliary", "Build", "vcvars64.bat");
    }

    // A cppfront set in Tools > Options keeps its include folder beside it, as
    // cppfront's own repository and the bundled one do
    static string IncludeDirFor(string cppfront)
    {
        var folder = Path.GetDirectoryName(cppfront);
        if (folder == null)
        {
            return Cppfront.BundledIncludeDir;
        }
        var beside = Path.Combine(folder, "include");
        var repository = Path.Combine(folder, "..", "include");
        return File.Exists(Path.Combine(beside, "cpp2util.h")) ? beside
            : File.Exists(Path.Combine(repository, "cpp2util.h")) ? Path.GetFullPath(repository)
            : Cppfront.BundledIncludeDir;
    }

    // %TEMP%\cpp2-tools\run\<hash of the source path>
    static string WorkDir(string source)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(source).ToUpperInvariant()));
        return Path.Combine(Path.GetTempPath(), "cpp2-tools", "run", string.Concat(hash.Take(6).Select(b => b.ToString("x2"))));
    }
}
