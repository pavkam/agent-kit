// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Architecture.Tests;

using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

using Microsoft.Build.Evaluation;

/// <summary>Represents an immutable evaluated graph of source-project references.</summary>
internal sealed class ProjectReferenceGraph
{
    /// <summary>Lists focused first-party implementations that may depend only on neutral contracts and diagnostics.</summary>
    private static readonly ImmutableHashSet<string> BehavioralRuntimes =
    [
        "AgentKit.Artifacts", "AgentKit.Budgets", "AgentKit.Context", "AgentKit.Context.Compaction",
        "AgentKit.Durability", "AgentKit.Goals", "AgentKit.Hooks", "AgentKit.Identity", "AgentKit.IO", "AgentKit.Loop",
        "AgentKit.Memory", "AgentKit.Output", "AgentKit.Permissions", "AgentKit.Providers", "AgentKit.Session", "AgentKit.Tools",
    ];

    /// <summary>Initializes a graph from an already validated immutable mapping.</summary>
    /// <param name="edges">The complete copied source-project mapping.</param>
    private ProjectReferenceGraph(ImmutableDictionary<string, ImmutableArray<string>> edges) => Edges = edges;

    /// <summary>Gets each source project and its distinct source-project dependencies.</summary>
    /// <value>An immutable mapping using project names without extensions.</value>
    internal ImmutableDictionary<string, ImmutableArray<string>> Edges { get; }

    /// <summary>Loads the current repository using the test assembly's captured build configuration.</summary>
    /// <returns>An immutable graph evaluated under the configuration that built this test assembly.</returns>
    /// <exception cref="ArgumentException">The captured configuration is blank.</exception>
    /// <exception cref="InvalidOperationException">The assembly lacks exactly one build-configuration metadata value, or the repository root cannot be located.</exception>
    internal static ProjectReferenceGraph LoadRepository()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = typeof(ProjectReferenceGraph).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "BuildConfiguration")
            .Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);
        return Load(repositoryRoot, configuration);
    }

    /// <summary>Locates the repository root by walking up from the running test assembly's own output directory.</summary>
    /// <returns>The absolute repository root directory.</returns>
    /// <remarks>
    /// This deliberately does not use <c>[CallerFilePath]</c>: Shouldly's <c>CapturePathMapsForShouldly</c> build
    /// target sets the C# compiler's <c>PathMap</c> under <c>ContinuousIntegrationBuild</c> (set automatically by
    /// most CI providers), which rewrites both PDB sequence points and <c>[CallerFilePath]</c>-substituted string
    /// constants to a synthetic path that does not exist on disk at runtime. <see cref="AppContext.BaseDirectory"/>
    /// is a runtime value, never subject to compile-time path mapping, so walking up from it is reliable in CI.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No ancestor directory of the test output contains <c>AgentKit.slnx</c>.</exception>
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgentKit.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (AgentKit.slnx) above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>Creates an immutable graph from explicit project-reference entries.</summary>
    /// <param name="edges">Project names paired with their referenced source-project names.</param>
    /// <returns>A graph after checking project and target identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="edges"/> or an entry value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A name is blank, duplicated, self-referencing, or missing from the graph.</exception>
    internal static ProjectReferenceGraph Create(IEnumerable<KeyValuePair<string, IEnumerable<string>>> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(StringComparer.Ordinal);
        foreach (var entry in edges)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Key);
            ArgumentNullException.ThrowIfNull(entry.Value);
            var targets = entry.Value.ToImmutableArray();
            if (targets.Any(string.IsNullOrWhiteSpace) || targets.Distinct(StringComparer.Ordinal).Count() != targets.Length || !builder.TryAdd(entry.Key, targets))
            {
                throw new ArgumentException($"Project '{entry.Key}' has blank or duplicate graph entries.", nameof(edges));
            }
        }

        foreach (var (source, targets) in builder)
        {
            if (targets.Contains(source, StringComparer.Ordinal) || targets.Any(target => !builder.ContainsKey(target)))
            {
                throw new ArgumentException($"Project '{source}' has a self-reference or missing source-project target.", nameof(edges));
            }
        }

        return new ProjectReferenceGraph(builder.ToImmutable());
    }

    /// <summary>Loads evaluated references for every project under a repository source directory.</summary>
    /// <param name="root">The repository root containing the source directory.</param>
    /// <param name="configuration">The MSBuild configuration used to evaluate conditional references.</param>
    /// <returns>An immutable graph of source projects.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An argument is blank or <paramref name="root"/> lacks a source directory.</exception>
    /// <exception cref="InvalidOperationException">A source project references a missing project or a project outside the source directory.</exception>
    internal static ProjectReferenceGraph Load(string root, string configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);
        var sourceDirectory = Path.Combine(Path.GetFullPath(root), "src");
        if (!Directory.Exists(sourceDirectory))
        {
            throw new ArgumentException("The repository root must contain a source directory.", nameof(root));
        }

        using var collection = new ProjectCollection(new Dictionary<string, string> { ["Configuration"] = configuration });
        var entries = new List<KeyValuePair<string, IEnumerable<string>>>();
        foreach (var path in Directory.EnumerateFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var project = collection.LoadProject(path);
            var referencedPaths = project.GetItems("ProjectReference")
                .Select(item => Path.GetFullPath(Path.Combine(project.DirectoryPath, item.EvaluatedInclude)))
                .ToArray();
            if (referencedPaths.Any(target => !target.StartsWith(sourceDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(target)))
            {
                throw new InvalidOperationException($"Source project '{project.FullPath}' has a missing or non-source project reference.");
            }

            var targets = referencedPaths
                .Select(target => Path.GetFileNameWithoutExtension(target))
                .ToArray();
            entries.Add(KeyValuePair.Create(Path.GetFileNameWithoutExtension(path), targets.AsEnumerable()));
        }

        collection.UnloadAllProjects();
        return Create(entries);
    }

    /// <summary>Rejects cycles and checked foundation or behavioral-runtime direction violations.</summary>
    /// <exception cref="InvalidOperationException">The graph contains a cycle or forbidden checked edge.</exception>
    internal void Validate()
    {
        foreach (var (source, targets) in Edges)
        {
            if (source == "AgentKit.Abstractions" && targets.Length != 0)
            {
                throw new InvalidOperationException("AgentKit.Abstractions must not reference source projects.");
            }

            if (source == "AgentKit" && targets.Any(target => target is not "AgentKit.Abstractions" and not "AgentKit.Observability"))
            {
                throw new InvalidOperationException("AgentKit may reference only abstractions and shared observability.");
            }

            if (source == "AgentKit.Observability" && targets.Any(target => target != "AgentKit.Abstractions"))
            {
                throw new InvalidOperationException("Shared observability may reference only abstractions.");
            }

            if (BehavioralRuntimes.Contains(source) && targets.Any(target => target is not "AgentKit.Abstractions" and not "AgentKit.Observability"))
            {
                throw new InvalidOperationException($"Behavioral runtime '{source}' references another implementation package.");
            }
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in Edges.Keys)
        {
            Visit(node, visiting, visited);
        }
    }

    /// <summary>Visits one node and rejects a back edge in the active traversal path.</summary>
    /// <param name="node">The project to visit.</param>
    /// <param name="visiting">Projects in the active depth-first path.</param>
    /// <param name="visited">Projects whose reachable paths have been checked.</param>
    private void Visit(string node, HashSet<string> visiting, HashSet<string> visited)
    {
        Debug.Assert(Edges.ContainsKey(node), "Every traversed project belongs to the validated graph.");
        if (visited.Contains(node))
        {
            return;
        }

        if (!visiting.Add(node))
        {
            throw new InvalidOperationException($"Project-reference cycle detected at '{node}'.");
        }

        foreach (var target in Edges[node])
        {
            Visit(target, visiting, visited);
        }
        _ = visiting.Remove(node);
        _ = visited.Add(node);
    }
}
