// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies ProcessOutputArtifactRejected behavior and contracts.</summary>
public sealed class ProcessOutputArtifactRejectedTests
{
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessOutputArtifactRejected(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var rejected = new ProcessOutputArtifactRejected("Preservation unavailable.");
        rejected.SafeMessage.ShouldBe("Preservation unavailable.");
        ProcessOutputArtifactResult result = rejected;
        _ = result.ShouldBeOfType<ProcessOutputArtifactRejected>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ProcessOutputArtifactRejected("Preservation unavailable.");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
