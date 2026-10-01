// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Architecture.Tests;

/// <summary>
/// Enforces the AGENTS.md rule that AgentKit keeps no backwards-compatibility surface: no obsolete member, <c>Legacy*</c>
/// identifier, obsolete-warning suppression, or compatibility-created remark may be added, and the checked-in baseline of
/// remaining occurrences may only shrink.
/// </summary>
/// <remarks>
/// The rule is structural, so the guard reads syntax. <c>ObsoleteSurfaceBaseline.txt</c> lists the remaining occurrences per
/// file and category; <c>ObsoleteSurfaceAllowList.txt</c> lists occurrences that describe external or domain facts and stay
/// on purpose, each with a written reason. Neither file may be loosened to make a change pass.
/// </remarks>
public sealed class ObsoleteSurfaceTests
{
    /// <summary>Verifies no file carries more superseded-shape evidence than the checked-in baseline permits.</summary>
    [Fact]
    public void ScanRepository_WhenSourceTestsAndExamplesAreScanned_NeverExceedsTheBaseline()
    {
        var root = ObsoleteSurfaceInventory.FindRepositoryRoot();
        var actual = ObsoleteSurfaceInventory.ScanRepository(root);
        var baseline = ObsoleteSurfaceInventory.LoadBaseline(root);
        var allowed = ObsoleteSurfaceInventory.LoadAllowList(root);

        ObsoleteSurfaceInventory.CountScannedFiles(root).ShouldBeGreaterThan(0, "the scan found no files; the scan roots are stale");
        var violations = actual
            .Where(pair => !allowed.ContainsKey(pair.Key))
            .Where(pair => pair.Value > baseline.GetValueOrDefault(pair.Key))
            .Select(pair => $"{pair.Key.Path} [{pair.Key.Category}] has {pair.Value}, baseline permits {baseline.GetValueOrDefault(pair.Key)}")
            .ToArray();

        violations.ShouldBeEmpty(
            $"AgentKit keeps no compatibility surface; delete or replace in place instead of adding:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    /// <summary>Verifies the baseline is exact, so every removal is locked in by shrinking it.</summary>
    [Fact]
    public void ScanRepository_WhenEvidenceWasRemoved_RequiresTheBaselineToShrinkToMatch()
    {
        var root = ObsoleteSurfaceInventory.FindRepositoryRoot();
        var actual = ObsoleteSurfaceInventory.ScanRepository(root);
        var baseline = ObsoleteSurfaceInventory.LoadBaseline(root);

        var stale = baseline
            .Where(pair => actual.GetValueOrDefault(pair.Key) < pair.Value)
            .Select(pair => $"{ObsoleteSurfaceInventory.FormatBaselineLine(pair.Key, pair.Value)} -> now {actual.GetValueOrDefault(pair.Key)}")
            .ToArray();

        stale.ShouldBeEmpty(
            $"Evidence was removed; shrink ObsoleteSurfaceBaseline.txt to the new counts (delete the line at zero):{Environment.NewLine}{string.Join(Environment.NewLine, stale)}");
    }

    /// <summary>Verifies an allow-list entry still matches something and is not also baselined.</summary>
    [Fact]
    public void LoadAllowList_WhenEntriesAreReviewed_EachStillMatchesAndIsNotBaselined()
    {
        var root = ObsoleteSurfaceInventory.FindRepositoryRoot();
        var actual = ObsoleteSurfaceInventory.ScanRepository(root);
        var baseline = ObsoleteSurfaceInventory.LoadBaseline(root);

        var stale = ObsoleteSurfaceInventory.LoadAllowList(root)
            .Keys
            .Where(key => !actual.ContainsKey(key) || baseline.ContainsKey(key))
            .Select(key => $"{key.Path} [{key.Category}]")
            .ToArray();

        stale.ShouldBeEmpty($"Allow-list entries without a match, or duplicated in the baseline:{Environment.NewLine}{string.Join(Environment.NewLine, stale)}");
    }

    /// <summary>Verifies the scanner recognises each obsolete-attribute spelling.</summary>
    /// <param name="content">The text containing one attribute application.</param>
    [Theory]
    [InlineData("[Obsolete(\"x\")]\npublic sealed class A;")]
    [InlineData("[System.Obsolete]\npublic sealed class A;")]
    [InlineData("[global::System.Obsolete(\"x\", true)]\npublic sealed class A;")]
    public void Scan_WhenTextAppliesAnObsoleteAttribute_CountsIt(string content)
    {
        var counts = ObsoleteSurfaceScanner.Scan(content);

        counts[ObsoleteSurfaceCategory.ObsoleteAttribute].ShouldBe(1);
    }

    /// <summary>Verifies a new obsolete member fails the guard's counting.</summary>
    [Fact]
    public void Scan_WhenTextAddsAnObsoleteMember_ReportsExactlyOneAttribute()
    {
        var counts = ObsoleteSurfaceScanner.Scan("public sealed class A\n{\n    [Obsolete(\"Use B.\")]\n    public void Old() { }\n}\n");

        counts.Count.ShouldBe(1);
        counts[ObsoleteSurfaceCategory.ObsoleteAttribute].ShouldBe(1);
    }

    /// <summary>Verifies a new <c>Legacy*</c> identifier fails the guard's counting.</summary>
    [Fact]
    public void Scan_WhenTextDeclaresALegacyIdentifier_CountsEachOccurrence()
    {
        var counts = ObsoleteSurfaceScanner.Scan("public sealed record LegacyFileRead; var x = new LegacyFileRead(); IOldLegacyHost h;");

        counts[ObsoleteSurfaceCategory.LegacyIdentifier].ShouldBe(3);
    }

    /// <summary>Verifies prose and protocol vocabulary that is not a <c>Legacy*</c> identifier is not counted.</summary>
    /// <param name="content">The text that must not count.</param>
    [Theory]
    [InlineData("McpProtocolEra.Legacy,")]
    [InlineData("the legacy v1 dialect")]
    [InlineData("Legacy revisions establish negotiated state")]
    [InlineData("#pragma warning disable CS0168")]
    [InlineData("[ObsoleteName]")]
    public void Scan_WhenTextIsRetainedVocabulary_CountsNothing(string content) =>
        ObsoleteSurfaceScanner.Scan(content).ShouldBeEmpty();

    /// <summary>Verifies obsolete-warning suppressions are counted in every spelling.</summary>
    /// <param name="content">The suppression directive.</param>
    [Theory]
    [InlineData("#pragma warning disable CS0618 // reason")]
    [InlineData("#pragma warning disable CS0612")]
    [InlineData("#pragma  warning  disable CA1822, CS0618")]
    public void Scan_WhenTextSuppressesObsoleteWarnings_CountsTheDirective(string content) =>
        ObsoleteSurfaceScanner.Scan(content)[ObsoleteSurfaceCategory.ObsoleteSuppression].ShouldBe(1);

    /// <summary>Verifies compatibility-created and unpinned remarks are counted case-insensitively.</summary>
    [Fact]
    public void Scan_WhenTextDescribesCompatibilityCreatedOrUnpinnedValues_CountsBoth()
    {
        var counts = ObsoleteSurfaceScanner.Scan("null only for compatibility-created values; an Unpinned request");

        counts[ObsoleteSurfaceCategory.CompatibilityText].ShouldBe(2);
    }

    /// <summary>Verifies the scanner rejects a null text.</summary>
    [Fact]
    public void Scan_WhenContentIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ObsoleteSurfaceScanner.Scan(null!));

        exception.ParamName.ShouldBe("content");
    }

    /// <summary>Verifies generated output, non-source files, and the guard's own sources are never scanned.</summary>
    /// <param name="path">The repository-relative path.</param>
    /// <param name="expected">Whether the path is scanned.</param>
    [Theory]
    [InlineData("src/AgentKit/Foo.cs", true)]
    [InlineData("tests/AgentKit.Compatibility.Tests/Snapshots/AgentKit.verified.txt", true)]
    [InlineData("src/AgentKit/obj/Debug/Foo.cs", false)]
    [InlineData("tests/X/bin/Release/Foo.cs", false)]
    [InlineData("src/AgentKit/README.md", false)]
    [InlineData("tests/AgentKit.Architecture.Tests/ObsoleteSurfaceTests.cs", false)]
    public void IsScanned_WhenPathIsClassified_MatchesTheScanPolicy(string path, bool expected) =>
        ObsoleteSurfaceInventory.IsScanned(path).ShouldBe(expected);

    /// <summary>Verifies the baseline loader rejects a blank repository root before reading.</summary>
    [Fact]
    public void LoadBaseline_WhenRepositoryRootIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => ObsoleteSurfaceInventory.LoadBaseline(" "));

        exception.ParamName.ShouldBe("repositoryRoot");
    }
}
