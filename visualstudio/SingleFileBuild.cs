using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cpp2.VisualStudio;

/// <summary>Where a single-file build reads from and writes to.</summary>
/// <param name="VcVars">Visual Studio's vcvars64.bat, which sets up its x64 compiler.</param>
/// <param name="Cppfront">The cppfront to transpile with.</param>
/// <param name="Options">cppfront's options.</param>
/// <param name="IncludeDir">The folder with cpp2util.h that goes with that cppfront.</param>
/// <param name="Source">The .cpp2 file.</param>
/// <param name="WorkDir">Where the generated code, objects and program go.</param>
public sealed record SingleFileBuild(
    string VcVars, string Cppfront, IReadOnlyList<string> Options, string IncludeDir, string Source, string WorkDir)
{
    public string Name => Path.GetFileNameWithoutExtension(Source);

    public string GeneratedCode => Path.Combine(WorkDir, Name + ".cpp");

    public string Program => Path.Combine(WorkDir, Name + ".exe");

    public string ScriptPath => Path.Combine(WorkDir, "build.cmd");

    /// <summary>
    /// A cmd script that sets up the compiler, transpiles, and compiles and
    /// links a debug build. When the options make the code import std, it first
    /// builds the std module, once per compiler version, into
    /// %TEMP%\cpp2-tools\std\&lt;version&gt;.
    /// </summary>
    public string Script()
    {
        // Debug code, the same for the std module and the program, which must match
        const string Flags = "/nologo /std:c++latest /EHsc /MDd /Zi /Od";
        var importsStd = Cpp2.VisualStudio.Cppfront.ImportsStd(Options);
        var script = new StringBuilder();
        script.AppendLine("@echo off");
        script.AppendLine($"call {Quote(VcVars)} >nul || exit /b 1");
        script.AppendLine(string.Join(" ", new[] { Quote(Cppfront) }
            .Concat(Options.Select(ToolRunner.Quote))
            .Concat([Quote(Source), "-output", Quote(GeneratedCode)])) + " || exit /b 1");

        var stdReference = "";
        var stdObject = "";
        if (importsStd)
        {
            // cpp2util.h imports std.compat, which needs std
            script.AppendLine(@"set ""STD_DIR=%TEMP%\cpp2-tools\std\%VCToolsVersion%""");
            script.AppendLine(@"if not exist ""%STD_DIR%\std.compat.obj"" (");
            script.AppendLine(@"    mkdir ""%STD_DIR%"" 2>nul");
            script.AppendLine($@"    cl {Flags} /c ""%VCToolsInstallDir%modules\std.ixx"" /Fo""%STD_DIR%\std.obj"" /ifcOutput ""%STD_DIR%\std.ifc"" /Fd""%STD_DIR%\std.pdb"" || exit /b 1");
            script.AppendLine($@"    cl {Flags} /c /reference ""std=%STD_DIR%\std.ifc"" ""%VCToolsInstallDir%modules\std.compat.ixx"" /Fo""%STD_DIR%\std.compat.obj"" /ifcOutput ""%STD_DIR%\std.compat.ifc"" /Fd""%STD_DIR%\std.pdb"" || exit /b 1");
            script.AppendLine(")");
            stdReference = @" /reference ""std=%STD_DIR%\std.ifc"" /reference ""std.compat=%STD_DIR%\std.compat.ifc""";
            stdObject = @" ""%STD_DIR%\std.obj"" ""%STD_DIR%\std.compat.obj""";
        }

        // A folder for /Fo and /Fd ends in a backslash, doubled so it doesn't
        // escape the closing quote when cl splits its command line
        var outputFolder = $"\"{WorkDir}\\\\\"";
        script.AppendLine(
            $"cl {Flags} /I {Quote(IncludeDir)}{stdReference} {Quote(GeneratedCode)}{stdObject} " +
            $"/Fe{Quote(Program)} /Fo{outputFolder} /Fd{outputFolder} || exit /b 1");
        return script.ToString();
    }

    // Always quoted: paths in a cmd script may hold spaces or parentheses
    static string Quote(string path) => $"\"{path}\"";
}
