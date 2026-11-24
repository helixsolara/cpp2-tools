using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace Cpp2.VisualStudio;

/// <summary>
/// Tools > Options > Cpp2 > General: the cppfront and its options for building
/// without CMake. Build and Run Cpp2 File always uses them; Transpile Cpp2 File
/// uses them for a file that no CMake build's cpp2_sources.json lists.
/// </summary>
public sealed class Cpp2Options : DialogPage
{
    [Category("cppfront")]
    [DisplayName("cppfront path")]
    [Description("The cppfront executable for building without CMake. When empty, the one that comes with the extension is used.")]
    public string CppfrontPath { get; set; } = "";

    [Category("cppfront")]
    [DisplayName("Arguments")]
    [Description("cppfront options for building without CMake, separated by spaces, such as -pure-cpp2.")]
    public string Arguments { get; set; } = "";

    /// <summary>The cppfront to run: the one set here, else the bundled one.</summary>
    public string Cppfront() =>
        string.IsNullOrWhiteSpace(CppfrontPath) ? VisualStudio.Cppfront.BundledPath : CppfrontPath.Trim();

    public IReadOnlyList<string> CppfrontOptions() =>
        Arguments.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
}
