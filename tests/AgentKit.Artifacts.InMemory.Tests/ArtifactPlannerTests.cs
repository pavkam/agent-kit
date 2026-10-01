// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

using static ArtifactTestSupport;

/// <summary>Verifies the pure shared planner's argument checks and decisions over a consistent entry view.</summary>
public sealed class ArtifactPlannerTests
{
    private static readonly byte[] _content = "planned"u8.ToArray();

    [Fact]
    public void Plans_WhenArgumentsAreNull_ThrowNamingThem()
    {
        var state = new ArtifactStoreState();

        Should.Throw<ArgumentNullException>(() => ArtifactPlanner.PlanPrepare(null!, Prepare(_content), Now)).ParamName.ShouldBe("lookup");
        Should.Throw<ArgumentNullException>(() => ArtifactPlanner.PlanPrepare(state, null!, Now)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => ArtifactPlanner.PlanFinalize(null!, Publish(), Now)).ParamName.ShouldBe("lookup");
        Should.Throw<ArgumentNullException>(() => ArtifactPlanner.PlanFinalize(state, null!, Now)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => ArtifactPlanner.PlanAbort(null!, Abort())).ParamName.ShouldBe("lookup");
        Should.Throw<ArgumentNullException>(() => ArtifactPlanner.PlanAbort(state, null!)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => ArtifactPlanner.PlanRead(null!, Read(Reference()))).ParamName.ShouldBe("lookup");
        Should.Throw<ArgumentNullException>(() => ArtifactPlanner.PlanRead(state, null!)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => ArtifactPlanner.PlanDelete(null!, Delete(Reference()), Now)).ParamName.ShouldBe("lookup");
        Should.Throw<ArgumentNullException>(() => ArtifactPlanner.PlanDelete(state, null!, Now)).ParamName.ShouldBe("request");
    }

    [Fact]
    public void PlanPrepare_WhenValid_PlansOneEntryAndItsPayloadWithoutTouchingTheView()
    {
        var state = new ArtifactStoreState();

        var plan = ArtifactPlanner.PlanPrepare(state, Prepare(_content), Now);

        var entry = plan.Upserts.ShouldHaveSingleItem();
        entry.State.ShouldBe(ArtifactEntryState.Prepared);
        entry.Reference.ShouldBeNull();
        plan.Stage.ShouldNotBeNull().Content.ShouldBe([.. _content]);
        plan.Releases.ShouldBeEmpty();
        plan.Result.ShouldBe(entry.Receipt);
        state.Count.ShouldBe(0);
    }

    [Fact]
    public void PlanPrepare_WhenTheDeclaredHashDiffersFromTheVerifiedHash_RejectsWithAnIntegrityMismatch()
    {
        var plan = ArtifactPlanner.PlanPrepare(
            new ArtifactStoreState(), Prepare(_content, metadata: Metadata(_content, declared: new ContentHash("sha256:other"))), Now);

        plan.Result.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.IntegrityMismatch);
        plan.Upserts.ShouldBeEmpty();
        plan.Stage.ShouldBeNull();
    }

    [Fact]
    public void PlanPrepare_WhenNoHashWasDeclared_AcceptsTheVerifiedHash()
    {
        var declared = Metadata(_content);
        var undeclared = new ArtifactMetadata(
            declared.OwnerId, declared.MediaType, declared.DeclaredLength, null, declared.Classification, declared.Ownership,
            declared.Mutability, declared.Retention, null);

        var plan = ArtifactPlanner.PlanPrepare(new ArtifactStoreState(), Prepare(_content, metadata: undeclared), Now);

        _ = plan.Result.ShouldBeOfType<ArtifactStorePrepared>();
    }

    [Fact]
    public void PlanFinalize_WhenPreparationExpired_PlansAnAbortedEntryAndReleasesItsPayload()
    {
        var state = new ArtifactStoreState();
        state.Commit(ArtifactPlanner.PlanPrepare(state, Prepare(_content, lifetime: TimeSpan.FromSeconds(1)), Now).Upserts);

        var plan = ArtifactPlanner.PlanFinalize(state, Publish(), Now.AddMinutes(1));

        plan.Result.ShouldBeOfType<ArtifactStoreFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        plan.Upserts.ShouldHaveSingleItem().State.ShouldBe(ArtifactEntryState.Aborted);
        plan.Releases.ShouldHaveSingleItem().State.ShouldBe(ArtifactEntryState.Prepared);
    }

    [Fact]
    public void PlanFinalize_WhenPublished_StampsTheReferenceWithTheOperationInstantAndResolvedMetadata()
    {
        var state = new ArtifactStoreState();
        state.Commit(ArtifactPlanner.PlanPrepare(state, Prepare(_content), Now).Upserts);

        var plan = ArtifactPlanner.PlanFinalize(state, Publish(), Now.AddMinutes(2));

        var reference = plan.Result.ShouldBeOfType<ArtifactStoreFinalized>().Reference;
        reference.CreatedAt.ShouldBe(Now.AddMinutes(2));
        reference.Integrity.VerifiedAt.ShouldBe(Now.AddMinutes(2));
        plan.Upserts.ShouldHaveSingleItem().Reference.ShouldBe(reference);
    }

    [Fact]
    public void PlanAbort_WhenTheStateIsUndefined_ThrowsUnreachable()
    {
        var state = new ArtifactStoreState();
        state.Restore(Entry(ArtifactEntryState.Prepared) with { State = (ArtifactEntryState) 99 });

        _ = Should.Throw<System.Diagnostics.UnreachableException>(() => ArtifactPlanner.PlanAbort(state, Abort()));
    }

    [Fact]
    public void PlanRead_WhenTheReferenceTenantDiffersFromTheIdentity_RejectsAsNotFoundWithoutConsultingTheView()
    {
        var decision = ArtifactPlanner.PlanRead(new ArtifactStoreState(), Read(Reference(), Other));

        decision.Entry.ShouldBeNull();
        decision.Rejection.ShouldNotBeNull().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    private static ArtifactReference Reference() => new(
        Artifact(1), new ArtifactVersion("1"), new ArtifactDirectoryId("dir"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1),
        Identity.TenantId, new ArtifactOwnerId("owner"), Identity.PrincipalId, "text/plain", 1,
        new ArtifactIntegrity(new ContentHash("sha256:x"), Now), DataClassification.Internal, ArtifactOwnershipKind.Session,
        ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null, Now);

    private static ArtifactEntry Entry(ArtifactEntryState state) => new(
        Identity.TenantId, Preparation(1), Artifact(1), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1),
        Identity.PrincipalId, new ArtifactDirectoryId("dir"), Metadata(_content), FileSecurityBinding.ContentFingerprint(_content),
        new IdempotencyKey("k"), Now, Now.AddMinutes(5), state, null, null);
}
