using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Cpp2.VisualStudio;

/// <summary>How a build transpiles one Cpp2 file.</summary>
/// <param name="Cppfront">The cppfront executable the build uses.</param>
/// <param name="Options">cppfront's options for the file, such as -pure-cpp2.</param>
/// <param name="BuildOutput">The file the build generates from it.</param>
public sealed record BuildSource(string Cppfront, IReadOnlyList<string> Options, string BuildOutput);

/// <summary>
/// Reads the cpp2_sources.json files the Cpp2 CMake module writes into build
/// folders, and finds the one that lists a given source.
/// </summary>
public static class SourcesFile
{
    // Build folders looked for in the source's folder and each folder above it
    static readonly string[] BuildFolderPatterns = ["out\\build\\*", ".out\\build\\*", "build", "build\\*", "cmake-build-*"];

    /// <summary>
    /// Finds how the most recently configured build that lists sourcePath
    /// transpiles it, or returns null when no build lists it.
    /// </summary>
    public static BuildSource? Find(string sourcePath)
    {
        var source = Path.GetFullPath(sourcePath);
        var candidates = new List<(FileInfo File, BuildSource Source)>();
        for (var folder = Path.GetDirectoryName(source); folder != null; folder = Path.GetDirectoryName(folder))
        {
            foreach (var sourcesFile in SourcesFilesUnder(folder))
            {
                var listed = TryRead(sourcesFile.FullName, source);
                if (listed != null)
                {
                    candidates.Add((sourcesFile, listed));
                }
            }
        }
        return candidates.OrderByDescending(candidate => candidate.File.LastWriteTimeUtc).Select(candidate => candidate.Source).FirstOrDefault();
    }

    static IEnumerable<FileInfo> SourcesFilesUnder(string folder)
    {
        foreach (var pattern in BuildFolderPatterns)
        {
            foreach (var buildFolder in MatchingFolders(folder, pattern))
            {
                foreach (var file in SafeEnumerate(() => new DirectoryInfo(buildFolder).EnumerateFiles("cpp2_sources*.json")))
                {
                    yield return file;
                }
            }
        }
    }

    // Expands a pattern such as "out\build\*", where only the last part may hold a wildcard
    static IEnumerable<string> MatchingFolders(string folder, string pattern)
    {
        var parent = Path.Combine(folder, Path.GetDirectoryName(pattern) ?? "");
        var last = Path.GetFileName(pattern);
        if (!Directory.Exists(parent))
        {
            return [];
        }
        return SafeEnumerate(() => Directory.EnumerateDirectories(parent, last));
    }

    // Folders can vanish or deny access while they are searched; skip them
    static IEnumerable<T> SafeEnumerate<T>(Func<IEnumerable<T>> enumerate)
    {
        try
        {
            return enumerate().ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    static BuildSource? TryRead(string sourcesFile, string source)
    {
        Contents? contents;
        try
        {
            using var stream = File.OpenRead(sourcesFile);
            contents = new DataContractJsonSerializer(typeof(Contents)).ReadObject(stream) as Contents;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SerializationException)
        {
            return null;
        }
        if (contents?.Version != 1 || contents.Cppfront == null || contents.Sources == null)
        {
            return null;
        }

        var entry = contents.Sources.FirstOrDefault(entry => entry.Source != null && SamePath(entry.Source, source));
        if (entry?.Output == null)
        {
            return null;
        }
        return new BuildSource(
            Path.GetFullPath(contents.Cppfront),
            entry.Options ?? [],
            Path.GetFullPath(entry.Output));
    }

    static bool SamePath(string listed, string source) =>
        string.Equals(Path.GetFullPath(listed), source, StringComparison.OrdinalIgnoreCase);

    [DataContract]
    sealed class Contents
    {
        [DataMember(Name = "version")] public int Version { get; set; }
        [DataMember(Name = "cppfront")] public string? Cppfront { get; set; }
        [DataMember(Name = "sources")] public List<Entry>? Sources { get; set; }
    }

    [DataContract]
    sealed class Entry
    {
        [DataMember(Name = "source")] public string? Source { get; set; }
        [DataMember(Name = "output")] public string? Output { get; set; }
        [DataMember(Name = "options")] public string[]? Options { get; set; }
    }
}
