using System;
using System.IO;

namespace Cpp2.VisualStudio;

/// <summary>
/// Keeps a copy of the extension's MSBuild rules (Cpp2.props, Cpp2.targets,
/// Cpp2.xml) and bundled cppfront at a path that doesn't change between
/// versions, %LOCALAPPDATA%\cpp2-tools\msbuild, which is where a project's
/// Build Customizations should point. The extension's own folder changes with
/// every version, so a project pointing there would break on an update.
/// </summary>
public static class MSBuildRules
{
    public static string StableFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cpp2-tools", "msbuild");

    static string BundledFolder => Path.Combine(Path.GetDirectoryName(typeof(MSBuildRules).Assembly.Location)!, "MSBuild");

    /// <summary>
    /// Copies each bundled file that is missing or differs from the stable copy.
    /// A file in use (cppfront.exe during a build) is left for the next start.
    /// </summary>
    public static void Deploy() => Deploy(BundledFolder, StableFolder);

    public static void Deploy(string from, string to)
    {
        if (!Directory.Exists(from))
        {
            return;
        }
        foreach (var source in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, source.Substring(from.Length).TrimStart(Path.DirectorySeparatorChar));
            if (IsSame(source, target))
            {
                continue;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                // File.Copy keeps the last write time, which IsSame compares next time
                File.Copy(source, target, overwrite: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // In use or locked; the next start tries again
            }
        }
    }

    static bool IsSame(string source, string target)
    {
        var from = new FileInfo(source);
        var to = new FileInfo(target);
        return to.Exists && to.Length == from.Length && to.LastWriteTimeUtc == from.LastWriteTimeUtc;
    }
}
