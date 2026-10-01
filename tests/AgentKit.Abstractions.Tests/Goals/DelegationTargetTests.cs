// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies DelegationTarget, snapshots, and selection results.</summary>
public sealed class DelegationTargetTests
{
    private static DelegationTarget Make(ImmutableArray<string>? capabilities = null, AgentId? agent = null, ComponentId? source = null) => new(
        agent ?? new AgentId(Guid.NewGuid()), new AgentDefinitionRevision(0), source ?? new ComponentId("local"), capabilities ?? []);

    [Fact]
    public void Constructor_WhenAgentIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(agent: default(AgentId))).ParamName.ShouldBe("agentId");

    [Fact]
    public void Constructor_WhenSourceIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(source: default(ComponentId))).ParamName.ShouldBe("source");

    [Fact]
    public void Constructor_WhenACapabilityIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(capabilities: [" "])).ParamName.ShouldBe("capabilities");

    [Fact]
    public void Equality_WhenCapabilitiesMatchByContent_IsStructural()
    {
        var agent = new AgentId(Guid.NewGuid());

        Make(["a"], agent).ShouldBe(Make(["a"], agent));
    }

    [Fact]
    public void Snapshot_WhenTargetsAreDefaultOrContainNull_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new DelegationTargetSnapshot(new ComponentId("s"), default)).ParamName.ShouldBe("targets");
        Should.Throw<ArgumentException>(() => new DelegationTargetCatalogSnapshot([null!])).ParamName.ShouldBe("targets");
        Should.Throw<ArgumentException>(() => new DelegationTargetSnapshot(default, [])).ParamName.ShouldBe("source");
    }

    [Fact]
    public void Discovery_WhenIdentitiesAreDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new DelegationDiscoveryRequest(default, new SessionId(Guid.NewGuid()), TestSupport.GoalTestData.Profile)).ParamName.ShouldBe("parentAgentId");
        Should.Throw<ArgumentOutOfRangeException>(() => new DelegationDiscoveryRequest(new AgentId(Guid.NewGuid()), default, TestSupport.GoalTestData.Profile)).ParamName.ShouldBe("parentSessionId");
    }

    [Fact]
    public void Results_WhenArgumentsAreNull_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new DelegationTargetSelected(null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new DelegationTargetRejected(null!)).ParamName.ShouldBe("rejection");
        Should.Throw<ArgumentNullException>(() => new DelegationPolicyDenied(null!)).ParamName.ShouldBe("rejection");
    }

    [Fact]
    public void SelectionResults_WhenMessagesAreBlankOrKeysDefault_Throw()
    {
        Should.Throw<ArgumentException>(() => new GoalStoreSelectionRejected(" ")).ParamName.ShouldBe("safeMessage");
        Should.Throw<ArgumentException>(() => new DelegationDispatcherSelectionRejected("")).ParamName.ShouldBe("safeMessage");
        Should.Throw<ArgumentException>(() => new GoalJoinStrategySelectionRejected(" ")).ParamName.ShouldBe("safeMessage");
        Should.Throw<ArgumentException>(() => new GoalStoreSelected(default, null!)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentException>(() => new DelegationDispatcherSelected(default, null!)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new GoalJoinStrategySelected(null!)).ParamName.ShouldBe("strategy");
    }

    [Fact]
    public void GoalStoreFailure_WhenKindIsUndefinedOrMessageBlank_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalStoreFailure((GoalStoreFailureKind) 99, "m")).ParamName.ShouldBe("kind");
        Should.Throw<ArgumentException>(() => new GoalStoreFailure(GoalStoreFailureKind.Denied, " ")).ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void GoalStoreDescriptor_WhenNameOrAudienceIsBlank_Throws()
    {
        Should.Throw<ArgumentException>(() => new GoalStoreDescriptor(" ", new ComponentId("a"), true, true)).ParamName.ShouldBe("name");
        Should.Throw<ArgumentException>(() => new GoalStoreDescriptor("n", default, true, true)).ParamName.ShouldBe("securityAudience");
        Should.Throw<ArgumentException>(() => new DelegationDispatcherDescriptor(default, true, true)).ParamName.ShouldBe("securityAudience");
    }

    [Fact]
    public void Pages_WhenCursorOrItemsAreInvalid_Throw()
    {
        Should.Throw<ArgumentException>(() => new GoalPage(default, null)).ParamName.ShouldBe("items");
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalPage([], 0)).ParamName.ShouldBe("next");
    }

    [Fact]
    public void Budget_WhenReserveRequestIsInvalid_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new GoalBudgetReserveRequest(null!, new GoalBudget(1, 1, 0), null)).ParamName.ShouldBe("delegation");
        Should.Throw<ArgumentNullException>(() => new GoalBudgetSettleRequest(null!, GoalBudgetUsage.None)).ParamName.ShouldBe("reservation");
        Should.Throw<ArgumentNullException>(() => new GoalBudgetRejected(null!)).ParamName.ShouldBe("rejection");
        Should.Throw<ArgumentNullException>(() => new GoalBudgetReserved(null!)).ParamName.ShouldBe("reservation");
    }
}
