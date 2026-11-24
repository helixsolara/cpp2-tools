using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;

namespace Cpp2.VisualStudio;

/// <summary>
/// Build > Go To Generated Code (also on the editor's context menu): opens the
/// file the CMake build generated from the active .cpp2 or .h2 file, read-only,
/// at the code generated from the caret's line. The build's cpp2_sources.json
/// says where that file is.
/// </summary>
public sealed class OpenGeneratedCommand
{
    public const int CommandId = 0x0101;

    readonly AsyncPackage _package;
    readonly Cpp2Output _output;
    readonly DTE2 _dte;

    OpenGeneratedCommand(AsyncPackage package, Cpp2Output output, DTE2 dte)
    {
        _package = package;
        _output = output;
        _dte = dte;
    }

    public static async Task RegisterAsync(AsyncPackage package, Cpp2Output output, CancellationToken cancellationToken)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var commands = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
            ?? throw new InvalidOperationException("The menu command service is missing");
        var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2
            ?? throw new InvalidOperationException("The DTE service is missing");

        var command = new OpenGeneratedCommand(package, output, dte);
        var menuCommand = new OleMenuCommand(command.Execute, new CommandID(TranspileCommand.CommandSet, CommandId));
        menuCommand.BeforeQueryStatus += command.UpdateStatus;
        commands.AddCommand(menuCommand);
    }

    // Shows the command only while a Cpp2 file is the active document
    void UpdateStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var command = (OleMenuCommand)sender;
        command.Visible = command.Enabled = Cpp2Documents.IsCpp2(Cpp2Documents.ActivePath(_dte));
    }

    void Execute(object sender, EventArgs e)
    {
        _package.JoinableTaskFactory.RunAsync(() => OpenAsync(_package.DisposalToken))
            .FileAndForget("cpp2/opengenerated");
    }

    async Task OpenAsync(CancellationToken cancellationToken)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var document = _dte.ActiveDocument;
        var source = document?.FullName;
        if (document == null || source == null || !Cpp2Documents.IsCpp2(source))
        {
            return;
        }
        var caretLine = Cpp2Documents.CaretLine(document);
        var unsaved = !document.Saved;

        await TaskScheduler.Default;
        var build = SourcesFile.Find(source);
        var target = build != null && File.Exists(build.BuildOutput)
            ? new Target(build.BuildOutput, GeneratedCode.FindLine(File.ReadAllLines(build.BuildOutput), caretLine), IsStale(source, build.BuildOutput))
            : null;

        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (build == null)
        {
            Explain($"No CMake build lists {source}. Configure a build that uses the Cpp2 module, or use Transpile Cpp2 File.");
            return;
        }
        if (target == null)
        {
            Explain($"The build hasn't generated {build.BuildOutput} yet. Build the project first.");
            return;
        }
        OpenReadOnly(target.Path, target.Line);
        _dte.StatusBar.Text = unsaved || target.IsStale
            ? "This generated code is older than the Cpp2 file; build to update it."
            : $"Opened {target.Path}";
    }

    static bool IsStale(string source, string generated) =>
        File.GetLastWriteTimeUtc(generated) < File.GetLastWriteTimeUtc(source);

    void Explain(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _output.Start("Go To Generated Code");
        _output.WriteLine(message);
        _output.Show();
    }

    // Read-only in the editor only: the file stays writable, since the build rewrites it
    void OpenReadOnly(string path, int line)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        VsShellUtilities.OpenDocument(_package, path, Guid.Empty, out _, out _, out _, out IVsTextView view);
        if (view == null)
        {
            return;
        }
        if (ErrorHandler.Succeeded(view.GetBuffer(out var buffer)) && ErrorHandler.Succeeded(buffer.GetStateFlags(out var flags)))
        {
            buffer.SetStateFlags(flags | (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY);
        }
        var zeroBasedLine = line - 1;
        view.SetCaretPos(zeroBasedLine, 0);
        view.CenterLines(zeroBasedLine, 1);
    }

    sealed record Target(string Path, int Line, bool IsStale);
}
