// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

using AgentKit.Budgets.Storage;

/// <summary>Verifies BudgetScopeCreateTransition behavior and contracts.</summary>
public sealed class BudgetScopeCreateTransitionTests
{
    private static readonly BudgetDimension _dimension = new("transition.dimension");
    private static readonly BudgetUnit _unit = new("count");
    private static readonly BudgetUnit _otherUnit = new("bytes");

    [Fact]
    public void Replay_WhenRequestIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => BudgetScopeCreateTransition.Replay(null!, null, null)).ParamName.ShouldBe("request");

    [Fact]
    public void Replay_WhenExactlyOnePersistedValueIsPresent_ThrowsArgumentException()
    {
        var request = CreateRequest("replay-mismatch");
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), Address());
        Should.Throw<ArgumentException>(() => BudgetScopeCreateTransition.Replay(request, request, null)).ParamName.ShouldBe("persistedReference");
        Should.Throw<ArgumentException>(() => BudgetScopeCreateTransition.Replay(request, null, reference)).ParamName.ShouldBe("persistedReference");
    }

    [Fact]
    public void Replay_WhenNoPersistedBindingExists_ReturnsNull()
    {
        var request = CreateRequest("replay-new");
        BudgetScopeCreateTransition.Replay(request, null, null).ShouldBeNull();
    }

    [Fact]
    public void Replay_WhenPersistedRequestMatches_ReturnsCreatedWithThePersistedReference()
    {
        var request = CreateRequest("replay-match");
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), Address());
        var result = BudgetScopeCreateTransition.Replay(request, request, reference);
        var created = result.ShouldBeOfType<BudgetLedgerScopeCreated>();
        created.Scope.ShouldBeSameAs(reference);
    }

    [Fact]
    public void Replay_WhenPersistedRequestDiffers_ThrowsMutationConflict()
    {
        var request = CreateRequest("replay-conflict-a");
        var otherRequest = CreateRequest("replay-conflict-b");
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), Address());
        var exception = Should.Throw<BudgetLedgerMutationConflictException>(() => BudgetScopeCreateTransition.Replay(request, otherRequest, reference));
        exception.Message.ShouldContain("bound to different evidence");
    }

    [Fact]
    public void EvaluateDepth_WhenRequestIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => BudgetScopeCreateTransition.EvaluateDepth(null!, null)).ParamName.ShouldBe("request");

    [Fact]
    public void EvaluateDepth_WhenParentIsNull_ReturnsNull()
    {
        var request = CreateRequest("depth-root", admission: Admission(2));
        BudgetScopeCreateTransition.EvaluateDepth(request, null).ShouldBeNull();
    }

    [Fact]
    public void EvaluateDepth_WhenParentDepthIsBelowCapturedMaximum_ReturnsNull()
    {
        var request = CreateRequest("depth-ok", admission: Admission(4));
        var parent = RootParent(depth: 2);
        BudgetScopeCreateTransition.EvaluateDepth(request, parent).ShouldBeNull();
    }

    [Fact]
    public void EvaluateDepth_WhenParentDepthMeetsCapturedMaximum_ReturnsMaximumDepthRejection()
    {
        var request = CreateRequest("depth-exceeded", admission: Admission(2));
        var parent = RootParent(depth: 2);
        var rejected = BudgetScopeCreateTransition.EvaluateDepth(request, parent).ShouldNotBeNull();
        rejected.Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.MaximumDepthExceeded);
    }

    [Fact]
    public void EvaluateLimits_WhenRequestIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => BudgetScopeCreateTransition.EvaluateLimits(null!, null, [])).ParamName.ShouldBe("request");

    [Fact]
    public void EvaluateLimits_WhenDescriptorsIsDefault_ThrowsArgumentException()
    {
        var request = CreateRequest("limits-default-descriptors", limits: [Limit(_unit, 10, BudgetLimitKind.Hard)]);
        Should.Throw<ArgumentException>(() => BudgetScopeCreateTransition.EvaluateLimits(request, null, default)).ParamName.ShouldBe("descriptors");
    }

    [Fact]
    public void EvaluateLimits_WhenDescriptorsLengthDiffersFromLimits_ThrowsArgumentException()
    {
        var request = CreateRequest("limits-wrong-length", limits: [Limit(_unit, 10, BudgetLimitKind.Hard)]);
        Should.Throw<ArgumentException>(() => BudgetScopeCreateTransition.EvaluateLimits(request, null, [])).ParamName.ShouldBe("descriptors");
    }

    [Fact]
    public void EvaluateLimits_WhenNoDescriptorIsRegisteredForTheLimitDimension_ReturnsInvalidLimitRejection()
    {
        var request = CreateRequest("limits-missing-descriptor", limits: [Limit(_unit, 10, BudgetLimitKind.Hard)]);
        var rejected = BudgetScopeCreateTransition.EvaluateLimits(request, null, [null]).ShouldNotBeNull();
        rejected.Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.InvalidLimit);
    }

    [Fact]
    public void EvaluateLimits_WhenDescriptorDoesNotAllowTheLimitUnit_ReturnsInvalidLimitRejection()
    {
        var request = CreateRequest("limits-unsupported-unit", limits: [Limit(_unit, 10, BudgetLimitKind.Hard)]);
        var descriptor = Descriptor(_otherUnit);
        var rejected = BudgetScopeCreateTransition.EvaluateLimits(request, null, [descriptor]).ShouldNotBeNull();
        rejected.Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.InvalidLimit);
    }

    [Fact]
    public void EvaluateLimits_WhenAncestorLimitUsesADifferentUnitForTheSameDimension_ReturnsInvalidLimitRejection()
    {
        var request = CreateRequest("limits-ancestor-unit-child", limits: [Limit(_unit, 10, BudgetLimitKind.Hard)]);
        var ancestorRequest = CreateRequest("limits-ancestor-unit-parent", limits: [Limit(_otherUnit, 10, BudgetLimitKind.Hard)]);
        var parent = RootParent(depth: 1, request: ancestorRequest);
        var rejected = BudgetScopeCreateTransition.EvaluateLimits(request, parent, [Descriptor(_unit)]).ShouldNotBeNull();
        rejected.Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.InvalidLimit);
        rejected.Failure.SafeMessage.ShouldContain("ancestor limit");
    }

    [Fact]
    public void EvaluateLimits_WhenLimitWidensAnAncestorHardLimit_ReturnsLimitWiderThanAncestorRejection()
    {
        var request = CreateRequest("limits-widen-child", limits: [Limit(_unit, 10, BudgetLimitKind.Hard)]);
        var ancestorRequest = CreateRequest("limits-widen-parent", limits: [Limit(_unit, 5, BudgetLimitKind.Hard)]);
        var parent = RootParent(depth: 1, request: ancestorRequest);
        var rejected = BudgetScopeCreateTransition.EvaluateLimits(request, parent, [Descriptor(_unit)]).ShouldNotBeNull();
        rejected.Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.LimitWiderThanAncestor);
    }

    [Fact]
    public void EvaluateLimits_WhenAncestorLimitIsSoftAndNarrower_ReturnsNull()
    {
        var request = CreateRequest("limits-soft-child", limits: [Limit(_unit, 10, BudgetLimitKind.Hard)]);
        var ancestorRequest = CreateRequest("limits-soft-parent", limits: [Limit(_unit, 5, BudgetLimitKind.Soft)]);
        var parent = RootParent(depth: 1, request: ancestorRequest);
        BudgetScopeCreateTransition.EvaluateLimits(request, parent, [Descriptor(_unit)]).ShouldBeNull();
    }

    [Fact]
    public void EvaluateLimits_WhenEveryLimitIsValidAndCompatibleWithLineage_ReturnsNull()
    {
        var request = CreateRequest("limits-valid-child", limits: [Limit(_unit, 10, BudgetLimitKind.Hard)]);
        var ancestorRequest = CreateRequest("limits-valid-parent", limits: [Limit(_unit, 20, BudgetLimitKind.Hard)]);
        var parent = RootParent(depth: 1, request: ancestorRequest);
        BudgetScopeCreateTransition.EvaluateLimits(request, parent, [Descriptor(_unit)]).ShouldBeNull();
    }

    [Fact]
    public void PlanAccepted_WhenRequestIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => BudgetScopeCreateTransition.PlanAccepted(null!, null, new BudgetScopeId(Guid.NewGuid()), false, 1)).ParamName.ShouldBe("request");

    [Fact]
    public void PlanAccepted_WhenGeneratedIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var request = CreateRequest("plan-default-id");
        Should.Throw<ArgumentOutOfRangeException>(() => BudgetScopeCreateTransition.PlanAccepted(request, null, default, false, 1)).ParamName.ShouldBe("generatedId");
    }

    [Fact]
    public void PlanAccepted_WhenNextRevisionIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var request = CreateRequest("plan-nonpositive-revision");
        Should.Throw<ArgumentOutOfRangeException>(() => BudgetScopeCreateTransition.PlanAccepted(request, null, new BudgetScopeId(Guid.NewGuid()), false, 0)).ParamName.ShouldBe("nextRevision");
    }

    [Fact]
    public void PlanAccepted_WhenParentDepthMeetsCapturedMaximum_ThrowsArgumentOutOfRangeException()
    {
        var request = CreateRequest("plan-parent-depth", admission: Admission(2));
        var parent = RootParent(depth: 2);
        Should.Throw<ArgumentOutOfRangeException>(() => BudgetScopeCreateTransition.PlanAccepted(request, parent, new BudgetScopeId(Guid.NewGuid()), false, 1)).ParamName.ShouldBe("parent");
    }

    [Fact]
    public void PlanAccepted_WhenGeneratedIdAlreadyExists_ThrowsBudgetLedgerStateException()
    {
        var request = CreateRequest("plan-duplicate-id");
        var exception = Should.Throw<BudgetLedgerStateException>(() => BudgetScopeCreateTransition.PlanAccepted(request, null, new BudgetScopeId(Guid.NewGuid()), true, 1));
        exception.Message.ShouldContain("duplicate value");
    }

    [Fact]
    public void PlanAccepted_WhenAcceptedWithoutAParent_ReturnsRootDepthAndNullParentScopeId()
    {
        var request = CreateRequest("plan-root");
        var generatedId = new BudgetScopeId(Guid.NewGuid());
        var (result, mutation) = BudgetScopeCreateTransition.PlanAccepted(request, null, generatedId, false, 7);
        var created = result.ShouldBeOfType<BudgetLedgerScopeCreated>();
        created.Scope.Id.ShouldBe(generatedId);
        mutation.ParentScopeId.ShouldBeNull();
        mutation.Depth.ShouldBe(1);
        mutation.Revision.ShouldBe(7);
        mutation.Reference.ShouldBeSameAs(created.Scope);
        mutation.Request.ShouldBeSameAs(request);
    }

    [Fact]
    public void PlanAccepted_WhenAcceptedWithAParent_ReturnsIncrementedDepthAndParentScopeId()
    {
        var request = CreateRequest("plan-child", admission: Admission(4));
        var parent = RootParent(depth: 2);
        var generatedId = new BudgetScopeId(Guid.NewGuid());
        var (result, mutation) = BudgetScopeCreateTransition.PlanAccepted(request, parent, generatedId, false, 3);
        var created = result.ShouldBeOfType<BudgetLedgerScopeCreated>();
        created.Scope.Id.ShouldBe(generatedId);
        mutation.ParentScopeId.ShouldBe(parent.Reference.Id);
        mutation.Depth.ShouldBe(parent.Depth + 1);
    }

    private static BudgetScopeAddress Address() => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);

    private static BudgetScopeAdmission Admission(int maximumScopeDepth) => new(maximumScopeDepth, 32, TimeSpan.FromMinutes(5));

    private static BudgetLimit Limit(BudgetUnit unit, decimal value, BudgetLimitKind kind) => new(_dimension, value, unit, kind);

    private static BudgetDimensionDescriptor Descriptor(params BudgetUnit[] allowedUnits) => new(_dimension, BudgetAggregationKind.Sum, [.. allowedUnits]);

    private static BudgetLedgerScopeCreateRequest CreateRequest(
        string key,
        BudgetScopeId? parentScopeId = null,
        ImmutableArray<BudgetLimit> limits = default,
        BudgetScopeAdmission? admission = null) =>
        new(new BudgetScopeRequest(parentScopeId, Address(), limits.IsDefault ? [] : limits, new IdempotencyKey(key)), admission ?? Admission(8));

    private static ScopeCreateParent RootParent(int depth, BudgetLedgerScopeCreateRequest? request = null)
    {
        var parentRequest = request ?? CreateRequest($"root-parent-{Guid.NewGuid():N}");
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), Address());
        var ancestors = ImmutableArray.CreateBuilder<BudgetLedgerScopeCreateRequest>(depth);
        ancestors.Add(parentRequest);
        for (var index = 1; index < depth; index++)
        {
            ancestors.Add(CreateRequest($"root-parent-ancestor-{index}-{Guid.NewGuid():N}"));
        }

        return new ScopeCreateParent(reference, parentRequest, depth, ancestors.MoveToImmutable());
    }
}
