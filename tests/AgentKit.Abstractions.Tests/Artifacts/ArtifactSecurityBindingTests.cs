// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactSecurityBinding behavior and contracts.</summary>
public sealed class ArtifactSecurityBindingTests
{
    [Theory]
    [InlineData("artifact-resource", "id")]
    [InlineData("preparation-resource", "id")]
    [InlineData("prepare-artifact", "artifactId")]
    [InlineData("prepare-preparation", "preparationId")]
    [InlineData("finalize", "preparationId")]
    [InlineData("abort-preparation", "preparationId")]
    public void Binding_WhenIdentityIsEmpty_ThrowsExactParameter(string operation, string expectedParameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => InvokeWithEmptyIdentity(operation));
        exception.ParamName.ShouldBe(expectedParameter);
    }

    [Fact]
    public void PrepareFingerprint_WhenDirectoryIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), Version(), ProfileKey(), ProfileVersion(), TenantId(), PrincipalId(), default, Metadata(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)));
        exception.ParamName.ShouldBe("directoryId");
    }

    [Fact]
    public void PrepareFingerprint_WhenMetadataIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), Version(), ProfileKey(), ProfileVersion(), TenantId(), PrincipalId(), DirectoryId(), null!, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)));
        exception.ParamName.ShouldBe("metadata");
    }

    [Fact]
    public void AbortFingerprint_WhenReasonIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ArtifactSecurityBinding.AbortFingerprint(PreparationId(), (ArtifactAbortReason) 999));
        exception.ParamName.ShouldBe("reason");
    }

    [Fact]
    public void FinalizeFingerprint_WhenInputsAreEquivalent_IsStable() =>
        ArtifactSecurityBinding.FinalizeFingerprint(PreparationId()).ShouldBe(ArtifactSecurityBinding.FinalizeFingerprint(PreparationId()));

    [Fact]
    public void AbortFingerprint_WhenInputsAreEquivalent_IsStable() =>
        ArtifactSecurityBinding.AbortFingerprint(PreparationId(), ArtifactAbortReason.Cancelled).ShouldBe(ArtifactSecurityBinding.AbortFingerprint(PreparationId(), ArtifactAbortReason.Cancelled));

    [Theory]
    [InlineData("read")]
    [InlineData("delete")]
    public void ReferenceFingerprint_WhenReferenceIsNull_ThrowsExactParameter(string operation)
    {
        var exception = Should.Throw<ArgumentNullException>(() => operation == "read" ? ArtifactSecurityBinding.ReadFingerprint(null!) : ArtifactSecurityBinding.DeleteFingerprint(null!));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void PrepareFingerprint_WhenInputsAreEquivalent_IsStable()
    {
        var first = PrepareFingerprint();
        var second = PrepareFingerprint();
        second.ShouldBe(first);
    }

    [Fact]
    public void PrepareFingerprint_WhenTimestampsRepresentSameInstants_IsStable()
    {
        var utc = PrepareFingerprint(createdAt: new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero), expiresAt: new DateTimeOffset(2026, 9, 7, 13, 0, 0, TimeSpan.Zero));
        var offset = PrepareFingerprint(createdAt: new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.FromHours(2)), expiresAt: new DateTimeOffset(2026, 9, 7, 15, 0, 0, TimeSpan.FromHours(2)));
        offset.ShouldBe(utc);
    }

    [Theory]
    [InlineData("version", "version", typeof(ArgumentNullException))]
    [InlineData("profile-key", "profileKey", typeof(ArgumentNullException))]
    [InlineData("profile-version", "profileVersion", typeof(ArgumentOutOfRangeException))]
    [InlineData("tenant", "tenantId", typeof(ArgumentNullException))]
    [InlineData("creator", "createdBy", typeof(ArgumentNullException))]
    [InlineData("duration", "expiresAt", typeof(ArgumentOutOfRangeException))]
    public void PrepareFingerprint_WhenBoundValueIsInvalid_ThrowsExactParameter(string field, string expectedParameter, Type expectedExceptionType)
    {
        Action action = field switch
        {
            "version" => () => _ = ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), default, ProfileKey(), ProfileVersion(), TenantId(), PrincipalId(), DirectoryId(), Metadata(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)),
            "profile-key" => () => _ = ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), Version(), default, ProfileVersion(), TenantId(), PrincipalId(), DirectoryId(), Metadata(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)),
            "profile-version" => () => _ = ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), Version(), ProfileKey(), default, TenantId(), PrincipalId(), DirectoryId(), Metadata(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)),
            "tenant" => () => _ = ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), Version(), ProfileKey(), ProfileVersion(), default, PrincipalId(), DirectoryId(), Metadata(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)),
            "creator" => () => _ = ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), Version(), ProfileKey(), ProfileVersion(), TenantId(), default, DirectoryId(), Metadata(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)),
            "duration" => () => _ = ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), Version(), ProfileKey(), ProfileVersion(), TenantId(), PrincipalId(), DirectoryId(), Metadata(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
        var exception = Should.Throw<ArgumentException>(action);
        exception.GetType().ShouldBe(expectedExceptionType);
        exception.ParamName.ShouldBe(expectedParameter);
    }

    [Fact]
    public void PrepareFingerprint_WhenAddressOrMetadataChanges_ChangesEvidence()
    {
        var baseline = PrepareFingerprint();
        var mutations = new[]
        {
            PrepareFingerprint(artifactId: new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000099"))),
            PrepareFingerprint(preparationId: new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000099"))),
            PrepareFingerprint(version: new ArtifactVersion("2")),
            PrepareFingerprint(profileKey: new ArtifactProfileKey("other")),
            PrepareFingerprint(profileVersion: new ArtifactProfileVersion(2)),
            PrepareFingerprint(tenantId: new TenantId("other")),
            PrepareFingerprint(createdBy: new PrincipalId("other")),
            PrepareFingerprint(directoryId: new ArtifactDirectoryId("other")),
            PrepareFingerprint(metadata: Metadata(mediaType: "application/json")),
            PrepareFingerprint(metadata: Metadata(legalHold: true)),
            PrepareFingerprint(createdAt: DateTimeOffset.UnixEpoch.AddSeconds(1), expiresAt: DateTimeOffset.UnixEpoch.AddMinutes(5).AddSeconds(1)),
            PrepareFingerprint(expiresAt: DateTimeOffset.UnixEpoch.AddMinutes(6)),
        };
        mutations.ShouldAllBe(fingerprint => fingerprint != baseline);
    }

    [Fact]
    public void ReferenceFingerprint_WhenReferencesAreEquivalent_IsStablePerOperation()
    {
        ArtifactSecurityBinding.ReadFingerprint(Reference()).ShouldBe(ArtifactSecurityBinding.ReadFingerprint(Reference()));
        ArtifactSecurityBinding.DeleteFingerprint(Reference()).ShouldBe(ArtifactSecurityBinding.DeleteFingerprint(Reference()));
        ArtifactSecurityBinding.ReadFingerprint(Reference()).ShouldNotBe(ArtifactSecurityBinding.DeleteFingerprint(Reference()));
    }

    [Fact]
    public void ReferenceFingerprint_WhenSecurityRelevantFieldChanges_ChangesReadAndDeleteEvidence()
    {
        var baseline = Reference();
        var mutations = new[]
        {
            Reference(id: Guid.Parse("10000000-0000-0000-0000-000000000099")),
            Reference(version: "2"),
            Reference(directoryId: "other"),
            Reference(profileKey: "other"),
            Reference(profileVersion: 2),
            Reference(tenantId: "other"),
            Reference(ownerId: "other"),
            Reference(createdBy: "other"),
            Reference(mediaType: "application/json"),
            Reference(length: 8),
            Reference(contentHash: "other"),
            Reference(verifiedAt: DateTimeOffset.UnixEpoch.AddSeconds(1)),
            Reference(classification: ArtifactDataClassification.Restricted),
            Reference(ownership: ArtifactOwnershipKind.Run),
            Reference(mutability: ArtifactMutability.AppendOnly),
            Reference(retentionPolicy: "other"),
            Reference(retentionExpiresAt: DateTimeOffset.UnixEpoch.AddDays(1)),
            Reference(legalHold: true),
            Reference(createdAt: DateTimeOffset.UnixEpoch.AddSeconds(1)),
        };
        mutations.ShouldAllBe(reference => ArtifactSecurityBinding.ReadFingerprint(reference) != ArtifactSecurityBinding.ReadFingerprint(baseline));
        mutations.ShouldAllBe(reference => ArtifactSecurityBinding.DeleteFingerprint(reference) != ArtifactSecurityBinding.DeleteFingerprint(baseline));
    }

    private static void InvokeWithEmptyIdentity(string operation)
    {
        _ = operation switch
        {
            "artifact-resource" => ArtifactSecurityBinding.ArtifactResource(default).ToString(),
            "preparation-resource" => ArtifactSecurityBinding.PreparationResource(default).ToString(),
            "prepare-artifact" => ArtifactSecurityBinding.PrepareFingerprint(default, PreparationId(), Version(), ProfileKey(), ProfileVersion(), TenantId(), PrincipalId(), DirectoryId(), Metadata(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)).ToString(),
            "prepare-preparation" => ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), default, Version(), ProfileKey(), ProfileVersion(), TenantId(), PrincipalId(), DirectoryId(), Metadata(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)).ToString(),
            "finalize" => ArtifactSecurityBinding.FinalizeFingerprint(default).ToString(),
            "abort-preparation" => ArtifactSecurityBinding.AbortFingerprint(default, ArtifactAbortReason.Cancelled).ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }

    private static ArtifactMetadata Metadata(string mediaType = "text/plain", bool legalHold = false) => new(new ArtifactOwnerId("owner"), mediaType, 7, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("retention"), null, legalHold));
    private static InputFingerprint PrepareFingerprint(ArtifactId? artifactId = null, ArtifactPreparationId? preparationId = null, ArtifactVersion? version = null, ArtifactProfileKey? profileKey = null, ArtifactProfileVersion? profileVersion = null, TenantId? tenantId = null, PrincipalId? createdBy = null, ArtifactDirectoryId? directoryId = null, ArtifactMetadata? metadata = null, DateTimeOffset? createdAt = null, DateTimeOffset? expiresAt = null) => ArtifactSecurityBinding.PrepareFingerprint(artifactId ?? ArtifactId(), preparationId ?? PreparationId(), version ?? Version(), profileKey ?? ProfileKey(), profileVersion ?? ProfileVersion(), tenantId ?? TenantId(), createdBy ?? PrincipalId(), directoryId ?? DirectoryId(), metadata ?? Metadata(), createdAt ?? DateTimeOffset.UnixEpoch, expiresAt ?? DateTimeOffset.UnixEpoch.AddMinutes(5));
    private static ArtifactReference Reference(Guid? id = null, string version = "1", string directoryId = "output", string profileKey = "profile", long profileVersion = 1, string tenantId = "tenant", string ownerId = "owner", string createdBy = "principal", string mediaType = "text/plain", long length = 7, string contentHash = "hash", DateTimeOffset? verifiedAt = null, ArtifactDataClassification classification = ArtifactDataClassification.Internal, ArtifactOwnershipKind ownership = ArtifactOwnershipKind.Session, ArtifactMutability mutability = ArtifactMutability.Immutable, string retentionPolicy = "retention", DateTimeOffset? retentionExpiresAt = null, bool legalHold = false, DateTimeOffset? createdAt = null) => new(new ArtifactId(id ?? Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion(version), new ArtifactDirectoryId(directoryId), new ArtifactProfileKey(profileKey), new ArtifactProfileVersion(profileVersion), new TenantId(tenantId), new ArtifactOwnerId(ownerId), new PrincipalId(createdBy), mediaType, length, new ArtifactIntegrity(new ContentHash(contentHash), verifiedAt ?? DateTimeOffset.UnixEpoch), classification, ownership, mutability, new ArtifactRetention(new ArtifactRetentionPolicyKey(retentionPolicy), retentionExpiresAt, legalHold), createdAt ?? DateTimeOffset.UnixEpoch);
    private static ArtifactId ArtifactId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static ArtifactPreparationId PreparationId() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static ArtifactDirectoryId DirectoryId() => new("output");
    private static ArtifactVersion Version() => new("1");
    private static ArtifactProfileKey ProfileKey() => new("profile");
    private static ArtifactProfileVersion ProfileVersion() => new(1);
    private static TenantId TenantId() => new("tenant");
    private static PrincipalId PrincipalId() => new("principal");
    [Fact]
    public void ArtifactSecurityBinding_WhenVersionChanges_ChangesReadAndDeleteEvidence()
    {
        var first = ReferenceArtifactContracts(version: "1");
        var second = ReferenceArtifactContracts(version: "2");
        ArtifactSecurityBinding.ReadFingerprint(first).ShouldNotBe(ArtifactSecurityBinding.ReadFingerprint(second));
        ArtifactSecurityBinding.DeleteFingerprint(first).ShouldNotBe(ArtifactSecurityBinding.DeleteFingerprint(second));
    }

    private static ArtifactReference ReferenceArtifactContracts(ArtifactId? id = null, string version = "1") => new(id ?? new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion(version), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
