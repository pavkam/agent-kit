// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

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
        var exception = Should.Throw<ArgumentException>(() => ArtifactSecurityBinding.PrepareFingerprint(
            ArtifactId(), PreparationId(), default, Metadata()));

        exception.ParamName.ShouldBe("directoryId");
    }

    [Fact]
    public void PrepareFingerprint_WhenMetadataIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ArtifactSecurityBinding.PrepareFingerprint(
            ArtifactId(), PreparationId(), DirectoryId(), null!));

        exception.ParamName.ShouldBe("metadata");
    }

    [Fact]
    public void AbortFingerprint_WhenReasonIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ArtifactSecurityBinding.AbortFingerprint(
            PreparationId(), (ArtifactAbortReason) 999));

        exception.ParamName.ShouldBe("reason");
    }

    [Theory]
    [InlineData("read")]
    [InlineData("delete")]
    public void ReferenceFingerprint_WhenReferenceIsNull_ThrowsExactParameter(string operation)
    {
        var exception = Should.Throw<ArgumentNullException>(() => operation == "read"
            ? ArtifactSecurityBinding.ReadFingerprint(null!)
            : ArtifactSecurityBinding.DeleteFingerprint(null!));

        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void PrepareFingerprint_WhenInputsAreEquivalent_IsStable()
    {
        var first = ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), DirectoryId(), Metadata());
        var second = ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), DirectoryId(), Metadata());

        second.ShouldBe(first);
    }

    [Fact]
    public void PrepareFingerprint_WhenAddressOrMetadataChanges_ChangesEvidence()
    {
        var baseline = ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), DirectoryId(), Metadata());
        var mutations = new[]
        {
            ArtifactSecurityBinding.PrepareFingerprint(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000099")), PreparationId(), DirectoryId(), Metadata()),
            ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000099")), DirectoryId(), Metadata()),
            ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), new ArtifactDirectoryId("other"), Metadata()),
            ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), DirectoryId(), Metadata(mediaType: "application/json")),
            ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), PreparationId(), DirectoryId(), Metadata(legalHold: true)),
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
            "prepare-artifact" => ArtifactSecurityBinding.PrepareFingerprint(default, PreparationId(), DirectoryId(), Metadata()).ToString(),
            "prepare-preparation" => ArtifactSecurityBinding.PrepareFingerprint(ArtifactId(), default, DirectoryId(), Metadata()).ToString(),
            "finalize" => ArtifactSecurityBinding.FinalizeFingerprint(default).ToString(),
            "abort-preparation" => ArtifactSecurityBinding.AbortFingerprint(default, ArtifactAbortReason.Cancelled).ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }

    private static ArtifactMetadata Metadata(string mediaType = "text/plain", bool legalHold = false) => new(
        new ArtifactOwnerId("owner"), mediaType, 7, new ContentHash("hash"), ArtifactDataClassification.Internal,
        ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("retention"), null, legalHold));

    private static ArtifactReference Reference(
        Guid? id = null, string version = "1", string directoryId = "output", string profileKey = "profile", long profileVersion = 1,
        string tenantId = "tenant", string ownerId = "owner", string createdBy = "principal",
        string mediaType = "text/plain", long length = 7, string contentHash = "hash",
        DateTimeOffset? verifiedAt = null, ArtifactDataClassification classification = ArtifactDataClassification.Internal,
        ArtifactOwnershipKind ownership = ArtifactOwnershipKind.Session,
        ArtifactMutability mutability = ArtifactMutability.Immutable, string retentionPolicy = "retention",
        DateTimeOffset? retentionExpiresAt = null, bool legalHold = false, DateTimeOffset? createdAt = null) => new(
            new ArtifactId(id ?? Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion(version),
            new ArtifactDirectoryId(directoryId), new ArtifactProfileKey(profileKey),
            new ArtifactProfileVersion(profileVersion), new TenantId(tenantId), new ArtifactOwnerId(ownerId),
            new PrincipalId(createdBy), mediaType, length, new ArtifactIntegrity(new ContentHash(contentHash), verifiedAt ?? DateTimeOffset.UnixEpoch),
            classification, ownership, mutability, new ArtifactRetention(new ArtifactRetentionPolicyKey(retentionPolicy), retentionExpiresAt, legalHold),
            createdAt ?? DateTimeOffset.UnixEpoch);

    private static ArtifactId ArtifactId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static ArtifactPreparationId PreparationId() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static ArtifactDirectoryId DirectoryId() => new("output");
}
