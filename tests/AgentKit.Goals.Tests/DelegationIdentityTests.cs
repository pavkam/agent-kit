// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

public sealed class DelegationIdentityTests
{
    private static readonly GoalId _parent = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));

    [Fact]
    public void ChildGoalId_WhenSameInputs_IsDeterministic() =>
        DelegationIdentity.ChildGoalId(_parent, new IdempotencyKey("k")).ShouldBe(DelegationIdentity.ChildGoalId(_parent, new IdempotencyKey("k")));

    [Fact]
    public void ChildGoalId_WhenKeyOrParentDiffers_DiffersAndNeverCollidesAcrossKinds()
    {
        var key = new IdempotencyKey("k");

        DelegationIdentity.ChildGoalId(_parent, new IdempotencyKey("other")).ShouldNotBe(DelegationIdentity.ChildGoalId(_parent, key));
        DelegationIdentity.ChildGoalId(new GoalId(Guid.NewGuid()), key).ShouldNotBe(DelegationIdentity.ChildGoalId(_parent, key));
        DelegationIdentity.DelegationId(_parent, key).Value.ShouldNotBe(DelegationIdentity.ChildGoalId(_parent, key).Value);
    }

    [Fact]
    public void AttemptId_WhenSameInputs_IsDeterministic() =>
        DelegationIdentity.AttemptId(_parent, new IdempotencyKey("k")).ShouldBe(DelegationIdentity.AttemptId(_parent, new IdempotencyKey("k")));

    [Fact]
    public void CreationKey_WhenDerived_EmbedsParentAndKey() =>
        DelegationIdentity.CreationKey(_parent, new IdempotencyKey("k")).Value.ShouldContain("k");

    [Fact]
    public void Stamp_WhenRead_RoundTripsDepthAndDefaultsToZero()
    {
        GoalDepth.Read(ExtensionData.Empty).ShouldBe(0);
        GoalDepth.Read(GoalDepth.Stamp(ExtensionData.Empty, 3)).ShouldBe(3);
    }

    [Fact]
    public void Stamp_WhenDepthIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => GoalDepth.Stamp(ExtensionData.Empty, -1)).ParamName.ShouldBe("depth");
}
