using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Cpp2.VisualStudio;

/// <summary>
/// The Cpp2 package: the Transpile Cpp2 File and Go To Generated Code commands
/// and the options. The
/// grammar is registered by Cpp2.pkgdef and needs no code. The package loads in
/// the background once the shell is up, so the command can hide itself for
/// files that aren't Cpp2.
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageGuidString)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideOptionPage(typeof(Cpp2Options), "Cpp2", "General", 0, 0, true)]
[ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
public sealed class Cpp2Package : AsyncPackage
{
    public const string PackageGuidString = "52d64e4b-55e2-4f50-9e51-7b9d69829c8c";

    Cpp2Output? _output;

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var options = (Cpp2Options)GetDialogPage(typeof(Cpp2Options));
        _output = await Cpp2Output.CreateAsync(this, cancellationToken);
        await TranspileCommand.RegisterAsync(this, options, _output, cancellationToken);
        await OpenGeneratedCommand.RegisterAsync(this, _output, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _output?.Dispose();
        }
        base.Dispose(disposing);
    }
}
