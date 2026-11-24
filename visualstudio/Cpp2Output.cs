using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Cpp2.VisualStudio;

/// <summary>
/// Where the Cpp2 commands report: a "Cpp2" pane in the Output window for what
/// cppfront printed, and the Error List for its diagnostics. Call on the main thread.
/// </summary>
public sealed class Cpp2Output : IDisposable
{
    static readonly Guid PaneGuid = new("6b3c1f0e-2a4d-4e8b-9c51-0d7f3a92e4b6");

    readonly IVsOutputWindowPane _pane;
    readonly ErrorListProvider _errors;

    Cpp2Output(IVsOutputWindowPane pane, ErrorListProvider errors)
    {
        _pane = pane;
        _errors = errors;
    }

    public static async Task<Cpp2Output> CreateAsync(AsyncPackage package, CancellationToken cancellationToken)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var window = await package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow
            ?? throw new InvalidOperationException("The Output window service is missing");
        var paneGuid = PaneGuid;
        ErrorHandler.ThrowOnFailure(window.CreatePane(ref paneGuid, "Cpp2", fInitVisible: 1, fClearWithSolution: 1));
        ErrorHandler.ThrowOnFailure(window.GetPane(ref paneGuid, out var pane));
        var errors = new ErrorListProvider(package) { ProviderName = "Cpp2" };
        return new Cpp2Output(pane, errors);
    }

    /// <summary>Clears the pane and the Error List's Cpp2 entries for a new run.</summary>
    public void Start(string heading)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _pane.Clear();
        _errors.Tasks.Clear();
        WriteLine(heading);
    }

    public void WriteLine(string text)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _pane.OutputStringThreadSafe(text + Environment.NewLine);
    }

    public void Show()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _pane.Activate();
    }

    /// <summary>Lists diagnostics in the Error List, and shows it when there are errors.</summary>
    public void Report(IReadOnlyList<Diagnostic> diagnostics)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        foreach (var diagnostic in diagnostics)
        {
            var task = new ErrorTask
            {
                Category = TaskCategory.BuildCompile,
                ErrorCategory = diagnostic.IsError ? TaskErrorCategory.Error : TaskErrorCategory.Warning,
                Document = diagnostic.File,
                // The Error List counts from 0, cppfront from 1
                Line = Math.Max(diagnostic.Line - 1, 0),
                Column = Math.Max(diagnostic.Column - 1, 0),
                Text = diagnostic.Message,
            };
            task.Navigate += (_, _) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _errors.Navigate(task, VSConstants.LOGVIEWID.Code_guid);
            };
            _errors.Tasks.Add(task);
        }
        if (diagnostics.Count > 0)
        {
            _errors.Show();
        }
    }

    public void Dispose() => _errors.Dispose();
}
