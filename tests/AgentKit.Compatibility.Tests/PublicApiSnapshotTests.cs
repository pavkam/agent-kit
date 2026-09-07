// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

public sealed class PublicApiSnapshotTests
{
    public static IEnumerable<object[]> PackableProjects => PackableProjectCatalog.Load().Select(static project => new object[] { project });

    [Theory]
    [MemberData(nameof(PackableProjects))]
    public Task PublicApi_WhenPackableAssembly_HasApprovedSnapshot(PackableProject project)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{project.AssemblyName}.dll");
        File.Exists(assemblyPath).ShouldBeTrue($"The packable project '{project.ProjectPath}' must be referenced by the compatibility project.");
        var assembly = Assembly.LoadFrom(assemblyPath);
        var settings = new VerifySettings();
        settings.UseDirectory("Snapshots");
        settings.UseFileName(project.AssemblyName);

        return Verify(PublicApiExtractor.Generate(assembly), settings);
    }

    [Fact]
    public void SnapshotCoverage_WhenComparedWithPackableProjects_IsExact()
    {
        var snapshotDirectory = Path.Combine(Path.GetDirectoryName(CurrentFile.Path())!, "Snapshots");
        var expected = PackableProjectCatalog.Load()
            .Select(static project => project.AssemblyName)
            .ToHashSet(StringComparer.Ordinal);
        var actual = Directory.EnumerateFiles(snapshotDirectory, "*.verified.txt", SearchOption.TopDirectoryOnly)
            .Select(static path => Path.GetFileName(path)[..^".verified.txt".Length])
            .ToHashSet(StringComparer.Ordinal);
        var missing = expected.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var stale = actual.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        missing.ShouldBeEmpty("Every evaluated packable source project requires a checked-in public API snapshot.");
        stale.ShouldBeEmpty("Snapshots for removed or renamed packable projects must be deleted explicitly.");
    }
}
