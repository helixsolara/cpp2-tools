using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace Cpp2.VisualStudio;

/// <summary>
/// Tools > Options > Cpp2 > General. Used for a file that no CMake build's
/// cpp2_sources.json lists; a listed file is transpiled as its build does.
/// </summary>
public sealed class Cpp2Options : DialogPage
{
    [Category("cppfront")]
    [DisplayName("cppfront path")]
    [Description("The cppfront executable for files outside a CMake build that uses the Cpp2 module. When empty, cppfront is looked for on PATH.")]
    public string CppfrontPath { get; set; } = "";

    [Category("cppfront")]
    [DisplayName("Arguments")]
    [Description("cppfront options for files outside a CMake build, such as -pure-cpp2.")]
    public string Arguments { get; set; } = "";
}
