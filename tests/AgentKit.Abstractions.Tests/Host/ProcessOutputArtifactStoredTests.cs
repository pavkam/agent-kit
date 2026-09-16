// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies ProcessOutputArtifactStored and ProcessOutputArtifactResult behavior and contracts.</summary>
public sealed class ProcessOutputArtifactStoredTests
{
    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ProcessOutputArtifactStored(null!)).ParamName.ShouldBe("reference");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reference = HostTestData.ArtifactReference();
        var stored = new ProcessOutputArtifactStored(reference);
        stored.Reference.ShouldBeSameAs(reference);
        ProcessOutputArtifactResult result = stored;
        _ = result.ShouldBeOfType<ProcessOutputArtifactStored>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ProcessOutputArtifactStored(HostTestData.ArtifactReference());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
