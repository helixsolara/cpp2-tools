using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cpp2.VisualStudio;

/// <summary>Runs cppfront on one file.</summary>
public static class Cppfront
{
    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The cppfront the extension ships, in MSBuild\cppfront beside the
    /// extension's assembly, where the MSBuild rules find it too.
    /// </summary>
    public static string BundledPath =>
        Path.Combine(Path.GetDirectoryName(typeof(Cppfront).Assembly.Location)!, "MSBuild", "cppfront", "cppfront.exe");

    /// <summary>The include folder that goes with the bundled cppfront (cpp2util.h).</summary>
    public static string BundledIncludeDir => Path.Combine(Path.GetDirectoryName(BundledPath)!, "include");

    /// <summary>
    /// Transpiles source to output with the given options. cppfront runs in
    /// output's folder, where it leaves an empty cpp2_post_build.sh.
    /// </summary>
    public static Task<ToolResult> RunAsync(
        string cppfront, IReadOnlyList<string> options, string source, string output, CancellationToken cancellationToken)
    {
        var arguments = string.Join(" ", options.Concat([source, "-output", output]).Select(ToolRunner.Quote));
        return ToolRunner.RunAsync(cppfront, arguments, Path.GetDirectoryName(output)!, Timeout, cancellationToken);
    }

    /// <summary>Whether options make cppfront's output import std, so it needs the std module.</summary>
    public static bool ImportsStd(IReadOnlyList<string> options) =>
        options.Any(option => option is "-pure-cpp2" or "-p" or "-import-std" or "-im");
}
