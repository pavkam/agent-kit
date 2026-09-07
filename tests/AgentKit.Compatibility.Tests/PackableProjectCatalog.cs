// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

internal static class PackableProjectCatalog
{
    internal static IReadOnlyList<PackableProject> Load([CallerFilePath] string sourceFile = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", ".."));
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
}
