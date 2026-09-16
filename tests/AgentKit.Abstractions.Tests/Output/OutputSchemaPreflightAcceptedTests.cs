// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;
/// <summary>Verifies OutputSchemaPreflightAccepted behavior and contracts.</summary>
public sealed class OutputSchemaPreflightAcceptedTests
{
    [Fact]
    public void ClosedResults_WhenRequiredEvidenceIsNull_RejectExactParameter() => Should.Throw<ArgumentNullException>(() => new OutputSchemaPreflightAccepted(null!)).ParamName.ShouldBe("manifest");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsManifest()
    {
        var manifest = OutputTestData.PreflightManifest();
        var accepted = new OutputSchemaPreflightAccepted(manifest);
        accepted.Manifest.ShouldBe(manifest);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputSchemaPreflightAccepted(OutputTestData.PreflightManifest());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
