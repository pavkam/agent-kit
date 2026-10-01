// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Architecture.Tests;

using System.Collections.Immutable;
using System.Globalization;

/// <summary>Loads the repository inventory, the shrink-only baseline, and the retained-on-purpose allow-list.</summary>
internal static class ObsoleteSurfaceInventory
{
    private const string _baselineFile = "ObsoleteSurfaceBaseline.txt";
    private const string _allowListFile = "ObsoleteSurfaceAllowList.txt";
    private static readonly string[] _scannedRoots = ["src", "tests", "examples"];
    private static readonly string[] _scannedExtensions = [".cs", ".verified.txt"];
    private static readonly string[] _skippedSegments = ["bin", "obj", "TestResults", "node_modules"];

    /// <summary>Scans <c>src</c>, <c>tests</c>, and <c>examples</c> and returns every file's per-category evidence.</summary>
    /// <param name="repositoryRoot">The absolute repository root.</param>
    /// <returns>Evidence keyed by repository-relative forward-slash path and category; zero counts are absent.</returns>
    /// <exception cref="ArgumentException"><paramref name="repositoryRoot"/> is blank.</exception>
    internal static ImmutableSortedDictionary<(string Path, ObsoleteSurfaceCategory Category), int> ScanRepository(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var inventory = ImmutableSortedDictionary.CreateBuilder<(string Path, ObsoleteSurfaceCategory Category), int>(PathCategoryComparer.Instance);
        foreach (var root in _scannedRoots)
        {
            var directory = Path.Combine(repositoryRoot, root);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repositoryRoot, file).Replace(Path.DirectorySeparatorChar, '/');
                if (!IsScanned(relative))
                {
                    continue;
                }

                foreach (var (category, count) in ObsoleteSurfaceScanner.Scan(File.ReadAllText(file)))
                {
                    inventory[(relative, category)] = count;
                }
            }
        }

        return inventory.ToImmutable();
    }

    /// <summary>Counts the files the scan reads, so an empty violation set is distinguishable from stale scan roots.</summary>
    /// <param name="repositoryRoot">The absolute repository root.</param>
    /// <returns>The number of scanned source and snapshot files.</returns>
    /// <exception cref="ArgumentException"><paramref name="repositoryRoot"/> is blank.</exception>
    internal static int CountScannedFiles(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        return _scannedRoots
            .Select(root => Path.Combine(repositoryRoot, root))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            .Count(file => IsScanned(Path.GetRelativePath(repositoryRoot, file).Replace(Path.DirectorySeparatorChar, '/')));
    }

    /// <summary>Loads the checked-in shrink-only baseline.</summary>
    /// <param name="repositoryRoot">The absolute repository root.</param>
    /// <returns>Permitted counts keyed by path and category; an absent key permits zero.</returns>
    /// <exception cref="ArgumentException"><paramref name="repositoryRoot"/> is blank.</exception>
    /// <exception cref="FormatException">A baseline line is malformed.</exception>
    internal static ImmutableSortedDictionary<(string Path, ObsoleteSurfaceCategory Category), int> LoadBaseline(string repositoryRoot)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<(string Path, ObsoleteSurfaceCategory Category), int>(PathCategoryComparer.Instance);
        foreach (var fields in ReadRecords(repositoryRoot, _baselineFile, expectedFields: 3))
        {
            builder[(fields[0], ParseCategory(fields[1]))] = int.Parse(fields[2], CultureInfo.InvariantCulture);
        }

        return builder.ToImmutable();
    }

    /// <summary>Loads the retained-on-purpose allow-list.</summary>
    /// <param name="repositoryRoot">The absolute repository root.</param>
    /// <returns>Each exempt path and category with its mandatory written reason.</returns>
    /// <exception cref="ArgumentException"><paramref name="repositoryRoot"/> is blank.</exception>
    /// <exception cref="FormatException">An allow-list line is malformed or has a blank reason.</exception>
    internal static ImmutableSortedDictionary<(string Path, ObsoleteSurfaceCategory Category), string> LoadAllowList(string repositoryRoot)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<(string Path, ObsoleteSurfaceCategory Category), string>(PathCategoryComparer.Instance);
        foreach (var fields in ReadRecords(repositoryRoot, _allowListFile, expectedFields: 3))
        {
            if (string.IsNullOrWhiteSpace(fields[2]))
            {
                throw new FormatException($"Allow-list entry '{fields[0]}' has no reason.");
            }

            builder[(fields[0], ParseCategory(fields[1]))] = fields[2];
        }

        return builder.ToImmutable();
    }

    /// <summary>Formats one key as the exact checked-in baseline line.</summary>
    /// <param name="key">The path and category.</param>
    /// <param name="count">The count.</param>
    /// <returns>A tab-separated baseline line.</returns>
    internal static string FormatBaselineLine((string Path, ObsoleteSurfaceCategory Category) key, int count) =>
        $"{key.Path}\t{key.Category}\t{count.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Locates the repository root above the running test assembly.</summary>
    /// <returns>The absolute root containing <c>AgentKit.slnx</c>.</returns>
    /// <exception cref="InvalidOperationException">No ancestor contains the solution file.</exception>
    internal static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgentKit.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Could not locate the repository root above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>Decides whether one repository-relative path is scanned.</summary>
    /// <param name="relativePath">The forward-slash repository-relative path.</param>
    /// <returns>True for source and snapshot text outside generated directories and the guard's own files.</returns>
    internal static bool IsScanned(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (!_scannedExtensions.Any(extension => relativePath.EndsWith(extension, StringComparison.Ordinal)))
        {
            return false;
        }

        if (relativePath.Split('/').Any(segment => _skippedSegments.Contains(segment, StringComparer.Ordinal)))
        {
            return false;
        }

        // The guard's own sources spell the search patterns and fixtures it must recognise.
        return !relativePath.StartsWith("tests/AgentKit.Architecture.Tests/ObsoleteSurface", StringComparison.Ordinal);
    }

    private static IEnumerable<string[]> ReadRecords(string repositoryRoot, string fileName, int expectedFields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var path = Path.Combine(repositoryRoot, "tests", "AgentKit.Architecture.Tests", fileName);
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split('\t');
            yield return fields.Length == expectedFields
                ? fields
                : throw new FormatException($"'{fileName}' line '{line}' must have {expectedFields} tab-separated fields.");
        }
    }

    private static ObsoleteSurfaceCategory ParseCategory(string value) =>
        Enum.TryParse<ObsoleteSurfaceCategory>(value, ignoreCase: false, out var category) && Enum.IsDefined(category)
            ? category
            : throw new FormatException($"Unknown obsolete-surface category '{value}'.");

    private sealed class PathCategoryComparer: IComparer<(string Path, ObsoleteSurfaceCategory Category)>
    {
        internal static PathCategoryComparer Instance { get; } = new();

        public int Compare((string Path, ObsoleteSurfaceCategory Category) x, (string Path, ObsoleteSurfaceCategory Category) y)
        {
            var path = string.CompareOrdinal(x.Path, y.Path);
            return path != 0 ? path : x.Category.CompareTo(y.Category);
        }
    }
}
