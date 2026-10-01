// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

using static ArtifactTestSupport;

/// <summary>Verifies <see cref="ArtifactEntry"/> derived state and the planner-owned read decision.</summary>
public sealed class ArtifactEntryTests
{
    private static readonly byte[] _content = "entry"u8.ToArray();

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(2, false, false)]
    [InlineData(1, false, true)]
    [InlineData(1, true, false)]
    public void HoldsPayload_WhenStateAndTombstoneVary_OnlyLiveEntriesOwnBytes(int state, bool deleted, bool expected)
    {
        var entry = Create((ArtifactEntryState) state) with { DeletedAt = deleted ? Now : null };

        entry.HoldsPayload.ShouldBe(expected);
        entry.IsDeleted.ShouldBe(deleted);
    }

    [Fact]
    public void DerivedKeys_WhenRead_AreTenantQualifiedAndStable()
    {
        var entry = Create(ArtifactEntryState.Prepared);

        entry.PreparationKey.ShouldBe(new TenantArtifactPreparationKey(Identity.TenantId, Preparation(1)));
        entry.ReplayKey.ShouldBe(new ReplayKey(Identity.TenantId, "prepare", "k"));
        entry.ArtifactKey.ShouldBe(new TenantArtifactKey(Identity.TenantId, Artifact(1), new ArtifactVersion("1")));
        entry.Receipt.ShouldBe(new ArtifactStorePrepared(Preparation(1), Artifact(1), new ArtifactVersion("1"), Now.AddMinutes(5)));
    }

    [Fact]
    public void ArtifactReadDecision_WhenOpenedOrRejected_CarriesExactlyOneOutcome()
    {
        var entry = Create(ArtifactEntryState.Finalized);

        var open = ArtifactReadDecision.Open(entry);
        var rejected = ArtifactReadDecision.Reject(ArtifactFailureKind.NotFound, "missing");

        open.Entry.ShouldBe(entry);
        open.Rejection.ShouldBeNull();
        rejected.Entry.ShouldBeNull();
        rejected.Rejection.ShouldNotBeNull().Failure.ShouldBe(new ArtifactFailure(ArtifactFailureKind.NotFound, "missing"));
        Should.Throw<ArgumentNullException>(() => ArtifactReadDecision.Open(null!)).ParamName.ShouldBe("entry");
    }

    [Fact]
    public void ArtifactPlan_WhenConstructed_DefaultsToNoEffects()
    {
        var plan = new ArtifactPlan<string>("result");

        plan.Result.ShouldBe("result");
        plan.Upserts.ShouldBeEmpty();
        plan.Releases.ShouldBeEmpty();
        plan.Stage.ShouldBeNull();
    }

    private static ArtifactEntry Create(ArtifactEntryState state) => new(
        Identity.TenantId, Preparation(1), Artifact(1), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1),
        Identity.PrincipalId, new ArtifactDirectoryId("dir"), Metadata(_content), FileSecurityBinding.ContentFingerprint(_content),
        new IdempotencyKey("k"), Now, Now.AddMinutes(5), state, null, null);
}
