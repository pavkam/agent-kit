// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputAccepted behavior and contracts.</summary>
public sealed class OutputAcceptedTests
{
    [Fact]
    public void Constructor_WhenOutputIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputAccepted(null!, OutputTestData.Manifest())).ParamName.ShouldBe("output");

    [Fact]
    public void Constructor_WhenManifestIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputAccepted(OutputTestData.Candidate(), null!)).ParamName.ShouldBe("manifest");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var output = OutputTestData.Candidate();
        var manifest = OutputTestData.Manifest();
        var accepted = new OutputAccepted(output, manifest);
        accepted.Output.ShouldBe(output);
        accepted.Manifest.ShouldBe(manifest);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputAccepted(OutputTestData.Candidate(), OutputTestData.Manifest());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
