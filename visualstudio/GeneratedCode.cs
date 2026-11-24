using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cpp2.VisualStudio;

/// <summary>
/// Maps lines of a .cpp2 file to the code cppfront generated from it, through
/// the #line directives cppfront writes: after "#line N", the next line came
/// from source line N, the one after from N + 1, and so on.
/// </summary>
public static class GeneratedCode
{
    static readonly Regex LineDirective = new(@"^\s*#\s*line\s+(\d+)", RegexOptions.CultureInvariant);

    /// <summary>
    /// The one-based generated line for sourceLine: the last line generated from
    /// it (cppfront declares functions first and defines them later, and the
    /// definition is the one worth seeing), else the last line from the nearest
    /// source line before it, else 1.
    /// </summary>
    public static int FindLine(IReadOnlyList<string> generatedLines, int sourceLine)
    {
        var exact = 0;
        var nearest = 0;
        var nearestSourceLine = 0;
        int? mapped = null;
        for (var index = 0; index < generatedLines.Count; index++)
        {
            var directive = LineDirective.Match(generatedLines[index]);
            if (directive.Success)
            {
                mapped = int.Parse(directive.Groups[1].Value);
                continue;
            }
            if (mapped is not int current)
            {
                continue;
            }

            var generatedLine = index + 1;
            if (current == sourceLine)
            {
                exact = generatedLine;
            }
            else if (current < sourceLine && current >= nearestSourceLine)
            {
                nearestSourceLine = current;
                nearest = generatedLine;
            }
            mapped = current + 1;
        }
        return exact > 0 ? exact : nearest > 0 ? nearest : 1;
    }
}
