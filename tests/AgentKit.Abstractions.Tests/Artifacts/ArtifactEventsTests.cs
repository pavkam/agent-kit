// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies the artifact lifecycle event family and its shared identity validation.</summary>
public sealed class ArtifactEventsTests
{
    private static readonly ComponentKey<IArtifactCoordinator> _key = new("coordinator");
    private static readonly ArtifactProfileKey _profile = new("profile");
    private static readonly ArtifactVersion _version = new("1");

    [Fact]
    public void ArtifactPreparedEvent_WhenCreated_RetainsIdentityAndBaseEvidence()
    {
        var artifactEvent = new ArtifactPreparedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, ArtifactId, PreparationId, _version);
        Base(artifactEvent);
        artifactEvent.ArtifactId.ShouldBe(ArtifactId);
        artifactEvent.PreparationId.ShouldBe(PreparationId);
        artifactEvent.Version.ShouldBe(_version);
    }

    [Fact]
    public void ArtifactFinalizedEvent_WhenCreated_RetainsIdentityAndBaseEvidence()
    {
        var artifactEvent = new ArtifactFinalizedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, ArtifactId, _version, PreparationId);
        Base(artifactEvent);
        artifactEvent.ArtifactId.ShouldBe(ArtifactId);
        artifactEvent.Version.ShouldBe(_version);
        artifactEvent.PreparationId.ShouldBe(PreparationId);
    }

    [Fact]
    public void ArtifactAbortedEvent_WhenCreated_RetainsReasonAndAbsence()
    {
        var artifactEvent = new ArtifactAbortedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, PreparationId, ArtifactAbortReason.Expired, true);
        Base(artifactEvent);
        artifactEvent.PreparationId.ShouldBe(PreparationId);
        artifactEvent.Reason.ShouldBe(ArtifactAbortReason.Expired);
        artifactEvent.AlreadyAbsent.ShouldBeTrue();
    }

    [Fact]
    public void ArtifactDeletedEvent_WhenCreated_RetainsVersionAndAbsence()
    {
        var artifactEvent = new ArtifactDeletedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, ArtifactId, _version, false);
        Base(artifactEvent);
        artifactEvent.ArtifactId.ShouldBe(ArtifactId);
        artifactEvent.Version.ShouldBe(_version);
        artifactEvent.AlreadyAbsent.ShouldBeFalse();
    }

    [Fact]
    public void ArtifactReconciledEvent_WhenCreated_RetainsDisposition()
    {
        var artifactEvent = new ArtifactReconciledEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, PreparationId, ArtifactReconciliationDisposition.Collected);
        Base(artifactEvent);
        artifactEvent.PreparationId.ShouldBe(PreparationId);
        artifactEvent.Disposition.ShouldBe(ArtifactReconciliationDisposition.Collected);
    }

    [Fact]
    public void Events_WhenSharedIdentityIsBlank_ThrowExactParameters()
    {
        Should.Throw<ArgumentException>(() => new ArtifactPreparedEvent(default, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, ArtifactId, PreparationId, _version)).ParamName.ShouldBe("coordinatorKey");
        Should.Throw<ArgumentException>(() => new ArtifactPreparedEvent(_key, default, _profile, DateTimeOffset.UnixEpoch, ArtifactId, PreparationId, _version)).ParamName.ShouldBe("tenantId");
        Should.Throw<ArgumentException>(() => new ArtifactPreparedEvent(_key, Identity.TenantId, default, DateTimeOffset.UnixEpoch, ArtifactId, PreparationId, _version)).ParamName.ShouldBe("profileKey");
    }

    [Fact]
    public void Events_WhenSpecificValuesAreInvalid_ThrowExactParameters()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactPreparedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, default, PreparationId, _version)).ParamName.ShouldBe("artifactId");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactPreparedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, ArtifactId, default, _version)).ParamName.ShouldBe("preparationId");
        Should.Throw<ArgumentException>(() => new ArtifactPreparedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, ArtifactId, PreparationId, default)).ParamName.ShouldBe("version");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactFinalizedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, default, _version, PreparationId)).ParamName.ShouldBe("artifactId");
        Should.Throw<ArgumentException>(() => new ArtifactFinalizedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, ArtifactId, default, PreparationId)).ParamName.ShouldBe("version");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactFinalizedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, ArtifactId, _version, default)).ParamName.ShouldBe("preparationId");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactAbortedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, default, ArtifactAbortReason.Expired, false)).ParamName.ShouldBe("preparationId");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactAbortedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, PreparationId, (ArtifactAbortReason) 999, false)).ParamName.ShouldBe("reason");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactDeletedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, default, _version, false)).ParamName.ShouldBe("artifactId");
        Should.Throw<ArgumentException>(() => new ArtifactDeletedEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, ArtifactId, default, false)).ParamName.ShouldBe("version");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReconciledEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, default, ArtifactReconciliationDisposition.Collected)).ParamName.ShouldBe("preparationId");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReconciledEvent(_key, Identity.TenantId, _profile, DateTimeOffset.UnixEpoch, PreparationId, (ArtifactReconciliationDisposition) 999)).ParamName.ShouldBe("disposition");
    }

    private static void Base(ArtifactEvent artifactEvent)
    {
        artifactEvent.CoordinatorKey.ShouldBe(_key);
        artifactEvent.TenantId.ShouldBe(Identity.TenantId);
        artifactEvent.ProfileKey.ShouldBe(_profile);
        artifactEvent.OccurredAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }
}
