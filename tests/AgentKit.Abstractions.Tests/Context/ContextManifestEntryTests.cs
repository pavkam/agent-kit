// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextManifestEntry"/> invariants.</summary>
public sealed class ContextManifestEntryTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesEvidence()
    {
        var entry = new ContextManifestEntry(
            ContextTestData.Source(),
            ContextManifestDisposition.Included,
            new ContextCostEstimate(4, 1),
            "Included by test.",
            []);
        entry.Disposition.ShouldBe(ContextManifestDisposition.Included);
    }

    [Fact]
    public void Constructor_WhenReasonIsBlank_Throws() =>
        Should.Throw<ArgumentException>(() => new ContextManifestEntry(
            ContextTestData.Source(),
            ContextManifestDisposition.Omitted,
            new ContextCostEstimate(0, 0),
            " ",
            [])).ParamName.ShouldBe("reason");
}
