// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactReferenceCommitIntent"/> validation.</summary>
public sealed class ArtifactReferenceCommitIntentTests
{
    private static readonly ArtifactReferenceCommitIntentId _id = new(Guid.Parse("a0000000-0000-0000-0000-0000000000a1"));

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var pin = Pin();
        var intent = Create(pin: pin);
        intent.Id.ShouldBe(_id);
        intent.TenantId.ShouldBe(Identity.TenantId);
        intent.PreparationId.ShouldBe(PreparationId);
        intent.ArtifactId.ShouldBe(ArtifactId);
        intent.Version.ShouldBe(new ArtifactVersion("1"));
        intent.OwnerId.ShouldBe(new ArtifactOwnerId("session:owner"));
        intent.Pin.ShouldBe(pin);
        intent.State.ShouldBe(ArtifactReferenceCommitState.Pending);
        intent.RecordedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        intent.UpdatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Constructor_WhenIdIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(id: default(ArtifactReferenceCommitIntentId))).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenTenantIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Create(tenant: default(TenantId))).ParamName.ShouldBe("tenantId");

    [Fact]
    public void Constructor_WhenPreparationIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(preparation: default(ArtifactPreparationId))).ParamName.ShouldBe("preparationId");

    [Fact]
    public void Constructor_WhenArtifactIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(artifact: default(ArtifactId))).ParamName.ShouldBe("artifactId");

    [Fact]
    public void Constructor_WhenVersionIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Create(version: default(ArtifactVersion))).ParamName.ShouldBe("version");

    [Fact]
    public void Constructor_WhenOwnerIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Create(owner: default(ArtifactOwnerId))).ParamName.ShouldBe("ownerId");

    [Fact]
    public void Constructor_WhenPinIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ArtifactReferenceCommitIntent(_id, Identity.TenantId, PreparationId, ArtifactId, new ArtifactVersion("1"), new ArtifactOwnerId("o"), null!, ArtifactReferenceCommitState.Pending, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("pin");

    [Fact]
    public void Constructor_WhenPinCoversAnotherIntent_ThrowsExactParameter()
    {
        var other = new ArtifactPin(new ArtifactReferenceCommitIntentId(Guid.Parse("a0000000-0000-0000-0000-0000000000a2")), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1));
        Should.Throw<ArgumentException>(() => Create(pin: other)).ParamName.ShouldBe("pin");
    }

    [Fact]
    public void Constructor_WhenStateIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(state: (ArtifactReferenceCommitState) 999)).ParamName.ShouldBe("state");

    [Fact]
    public void Constructor_WhenUpdatedPrecedesRecorded_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(updatedAt: DateTimeOffset.UnixEpoch.AddSeconds(-1))).ParamName.ShouldBe("updatedAt");

    private static ArtifactPin Pin() => new(_id, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1));

    private static ArtifactReferenceCommitIntent Create(
        ArtifactReferenceCommitIntentId? id = null, TenantId? tenant = null, ArtifactPreparationId? preparation = null,
        ArtifactId? artifact = null, ArtifactVersion? version = null, ArtifactOwnerId? owner = null, ArtifactPin? pin = null,
        ArtifactReferenceCommitState state = ArtifactReferenceCommitState.Pending, DateTimeOffset? updatedAt = null) => new(
            id ?? _id, tenant ?? Identity.TenantId, preparation ?? PreparationId, artifact ?? ArtifactId,
            version ?? new ArtifactVersion("1"), owner ?? new ArtifactOwnerId("session:owner"), pin ?? Pin(), state,
            DateTimeOffset.UnixEpoch, updatedAt ?? DateTimeOffset.UnixEpoch);
}
