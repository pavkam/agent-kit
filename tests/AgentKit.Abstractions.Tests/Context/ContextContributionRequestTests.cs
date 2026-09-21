// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextContributionRequest"/> boundary guards.</summary>
public sealed class ContextContributionRequestTests
{
    [Fact]
    public void Constructor_WhenEvidenceIsValid_PreservesCoordinates() =>
        ContextTestData.ContributionRequest().RunId.ShouldNotBe(default);

    [Fact]
    public void Constructor_WhenAgentIsNull_ThrowsBeforeReadingOtherEvidence() =>
        Should.Throw<ArgumentNullException>(() => new ContextContributionRequest(
            null!,
            default,
            null,
            null!,
            default,
            default,
            default,
            null!,
            null!,
            null!,
            null!)).ParamName.ShouldBe("agent");
}
