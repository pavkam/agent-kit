// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Architecture.Tests;

using System.Text.RegularExpressions;

/// <summary>
/// Enforces the AGENTS.md observability rule that source-generated <c>LoggerMessage</c> methods use
/// "stable, package-owned event IDs": no two source packages may claim the same event identifier.
/// </summary>
/// <remarks>
/// This rule is explicitly structural, so the check reads syntax rather than runtime behavior. It recognises both the
/// positional form <c>[LoggerMessage(2000, ...)]</c> and the named form <c>[LoggerMessage(EventId = 2000, ...)]</c>.
/// </remarks>
public sealed partial class LoggerMessageEventIdTests
{
    /// <summary>Verifies every <c>LoggerMessage</c> event ID under <c>src/</c> is owned by exactly one package.</summary>
    [Fact]
    public void LoggerMessageEventIds_WhenScannedAcrossSourcePackages_AreOwnedByExactlyOnePackage()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        var owners = new Dictionary<int, HashSet<string>>();

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var package = Path.GetRelativePath(sourceRoot, file).Split(Path.DirectorySeparatorChar)[0];
            foreach (Match match in LoggerMessageAttribute().Matches(File.ReadAllText(file)))
            {
                var eventId = int.Parse(match.Groups["id"].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (!owners.TryGetValue(eventId, out var packages))
                {
                    packages = [];
                    owners[eventId] = packages;
                }

                _ = packages.Add(package);
            }
        }

        owners.Count.ShouldBeGreaterThan(0, "the scan found no LoggerMessage attributes; the pattern is stale");
        var collisions = owners
            .Where(static pair => pair.Value.Count > 1)
            .OrderBy(static pair => pair.Key)
            .Select(static pair => $"{pair.Key}: {string.Join(", ", pair.Value.Order())}")
            .ToArray();

        collisions.ShouldBeEmpty($"event IDs claimed by more than one package:{Environment.NewLine}{string.Join(Environment.NewLine, collisions)}");
    }

    [GeneratedRegex(@"\[LoggerMessage\(\s*(?:EventId\s*=\s*)?(?<id>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex LoggerMessageAttribute();

    private static string FindRepositoryRoot()
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
}
