// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentRunProfilePublicationUnavailable behavior and contracts.</summary>
public sealed class AgentRunProfilePublicationUnavailableTests
{
    [Fact]
    public void Constructor_WhenSafeReasonIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new AgentRunProfilePublicationUnavailable(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsSafeReason()
    {
        var unavailable = new AgentRunProfilePublicationUnavailable("unavailable");
        unavailable.SafeReason.ShouldBe("unavailable");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunProfilePublicationUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
