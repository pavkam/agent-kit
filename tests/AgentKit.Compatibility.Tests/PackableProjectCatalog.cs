// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

internal static class PackableProjectCatalog
{
    internal static IReadOnlyList<PackableProject> Load()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var configuration = typeof(PackableProjectCatalog).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "BuildConfiguration")
            .Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);
        using var projects = new ProjectCollection(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Configuration"] = configuration,
            ["TargetFramework"] = "net10.0",
        });

        var packableProjects = Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(path => LoadProject(projects, path))
            .Where(static project => project is not null)
            .Cast<PackableProject>()
            .OrderBy(static project => project.AssemblyName, StringComparer.Ordinal)
            .ToArray();
        var duplicateAssemblyNames = packableProjects
            .GroupBy(static project => project.AssemblyName, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => $"{group.Key}: {string.Join(", ", group.Select(project => project.ProjectPath))}")
            .ToArray();
        return duplicateAssemblyNames.Length > 0
            ? throw new InvalidOperationException($"Packable projects must have unique assembly names. Duplicates: {string.Join("; ", duplicateAssemblyNames)}")
            : packableProjects;
    }

    private static PackableProject? LoadProject(ProjectCollection projects, string projectPath)
    {
        var project = projects.LoadProject(projectPath);
        return bool.TryParse(project.GetPropertyValue("IsPackable"), out var isPackable) && isPackable
            ? new PackableProject(projectPath, project.GetPropertyValue("AssemblyName"))
            : null;
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
}
