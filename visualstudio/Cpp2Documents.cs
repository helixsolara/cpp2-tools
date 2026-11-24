using System;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace Cpp2.VisualStudio;

/// <summary>What the Cpp2 commands need to know about the active document.</summary>
public static class Cpp2Documents
{
    public static bool IsCpp2(string? path) =>
        path != null
        && (path.EndsWith(".cpp2", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".h2", StringComparison.OrdinalIgnoreCase));

    /// <summary>The active document's path, or null when there is none.</summary>
    public static string? ActivePath(DTE2 dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return dte.ActiveDocument?.FullName;
        }
        catch (ArgumentException)
        {
            // Some windows, such as the start page, throw rather than give no document
            return null;
        }
    }

    /// <summary>The one-based line of the caret in document, or 1 when it has no text selection.</summary>
    public static int CaretLine(Document document)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return document.Selection is TextSelection selection ? selection.ActivePoint.Line : 1;
    }
}
