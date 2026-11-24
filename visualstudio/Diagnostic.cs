using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Cpp2.VisualStudio;

/// <summary>An error or warning that cppfront or the compiler reported.</summary>
/// <param name="File">The file it names, as a full path.</param>
/// <param name="Line">One-based, or 0 when none was given.</param>
/// <param name="Column">One-based, or 0 when none was given.</param>
public sealed record Diagnostic(string File, int Line, int Column, bool IsError, string Message)
{
    // cppfront's "path(line,col): error: message" and MSVC's
    // "path(line[,col]): [fatal ]error C2065: message"
    static readonly Regex Pattern = new(
        @"^\s*(?<file>[^()]+?)\((?<line>\d+)(?:,(?<column>\d+))?\)\s*: (?:fatal )?(?<severity>error|warning)(?: (?<code>[A-Z]+\d+))?: (?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The diagnostics in a tool's output. Relative paths are taken from folder.</summary>
    public static IReadOnlyList<Diagnostic> Parse(string output, string folder)
    {
        var diagnostics = new List<Diagnostic>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Pattern.Match(line);
            if (!match.Success)
            {
                continue;
            }
            var file = match.Groups["file"].Value.Trim();
            var code = match.Groups["code"].Success ? match.Groups["code"].Value + ": " : "";
            diagnostics.Add(new Diagnostic(
                Path.IsPathRooted(file) ? Path.GetFullPath(file) : Path.GetFullPath(Path.Combine(folder, file)),
                int.Parse(match.Groups["line"].Value),
                match.Groups["column"].Success ? int.Parse(match.Groups["column"].Value) : 0,
                match.Groups["severity"].Value == "error",
                code + match.Groups["message"].Value));
        }
        return diagnostics;
    }
}
