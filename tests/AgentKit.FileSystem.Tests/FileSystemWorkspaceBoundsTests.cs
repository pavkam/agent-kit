// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

/// <summary>Verifies <see cref="FileSystemWorkspaceBounds"/> defaults and argument constraints.</summary>
public sealed class FileSystemWorkspaceBoundsTests
{
    /// <summary>Verifies the documented defaults.</summary>
    [Fact]
    public void Default_WhenRead_CarriesTheDocumentedCeilings()
    {
        var bounds = FileSystemWorkspaceBounds.Default;

        bounds.MaximumDirectorySnapshotEntries.ShouldBe(10_000);
        bounds.MaximumSearchDepth.ShouldBe(100);
        bounds.MaximumSearchFiles.ShouldBe(10_000);
        bounds.MaximumSearchBytes.ShouldBe(100L * 1024 * 1024);
        bounds.MaximumSearchMatches.ShouldBe(10_000);
        bounds.MaximumSearchLineBytes.ShouldBe(64 * 1024);
        bounds.MaximumSearchDuration.ShouldBe(TimeSpan.FromMinutes(1));
        bounds.MaximumPatchEntries.ShouldBe(100);
        bounds.MaximumPatchBytes.ShouldBe(50L * 1024 * 1024);
    }

    /// <summary>Verifies every ceiling must be positive, naming the offending parameter.</summary>
    /// <param name="parameter">The parameter set to an invalid value.</param>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData("maximumDirectorySnapshotEntries", 0)]
    [InlineData("maximumSearchDepth", -1)]
    [InlineData("maximumSearchFiles", 0)]
    [InlineData("maximumSearchBytes", 0)]
    [InlineData("maximumSearchMatches", -5)]
    [InlineData("maximumSearchLineBytes", 0)]
    [InlineData("maximumSearchDuration", 0)]
    [InlineData("maximumPatchEntries", 0)]
    [InlineData("maximumPatchBytes", -1)]
    public void Constructor_WhenACeilingIsNotPositive_ThrowsArgumentOutOfRangeException(string parameter, long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileSystemWorkspaceBounds(
            parameter == "maximumDirectorySnapshotEntries" ? (int) value : 1,
            parameter == "maximumSearchDepth" ? (int) value : 1,
            parameter == "maximumSearchFiles" ? (int) value : 1,
            parameter == "maximumSearchBytes" ? value : 1,
            parameter == "maximumSearchMatches" ? (int) value : 1,
            parameter == "maximumSearchLineBytes" ? (int) value : 1,
            parameter == "maximumSearchDuration" ? TimeSpan.FromTicks(value) : TimeSpan.FromSeconds(1),
            parameter == "maximumPatchEntries" ? (int) value : 1,
            parameter == "maximumPatchBytes" ? value : 1));

        exception.ParamName.ShouldBe(parameter);
    }

    /// <summary>Verifies the smallest legal ceilings are accepted.</summary>
    [Fact]
    public void Constructor_WhenEveryCeilingIsAtItsMinimum_Succeeds() =>
        Should.NotThrow(() => new FileSystemWorkspaceBounds(1, 1, 1, 1, 1, 1, TimeSpan.FromTicks(1), 1, 1));
}
