// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies AgentIdentityOptionsSnapshot behavior and contracts.</summary>
public sealed class AgentIdentityOptionsSnapshotTests
{
    [Theory]
    [InlineData(0, 0, 1, "maximumDelegationDepth")]
    [InlineData(1, -1, 1, "maximumClockSkew")]
    [InlineData(1, 0, 0, "maximumEvidenceLifetime")]
    public void AgentIdentityOptionsSnapshot_WhenConstraintIsInvalid_ThrowsWithParameterName(int depth, long skewTicks, long lifetimeTicks, string parameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentIdentityOptionsSnapshot(false, depth, TimeSpan.FromTicks(skewTicks), TimeSpan.FromTicks(lifetimeTicks)));
        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Properties_WhenConstructed_ExposeTheExactCapturedValues()
    {
        var snapshot = new AgentIdentityOptionsSnapshot(true, 5, TimeSpan.FromMinutes(1), TimeSpan.FromHours(2));

        snapshot.AllowAnonymous.ShouldBeTrue();
        snapshot.MaximumDelegationDepth.ShouldBe(5);
        snapshot.MaximumClockSkew.ShouldBe(TimeSpan.FromMinutes(1));
        snapshot.MaximumEvidenceLifetime.ShouldBe(TimeSpan.FromHours(2));
        snapshot.ToString().ShouldContain(nameof(AgentIdentityOptionsSnapshot));
        (snapshot with { }).ShouldBe(snapshot);
    }
}
