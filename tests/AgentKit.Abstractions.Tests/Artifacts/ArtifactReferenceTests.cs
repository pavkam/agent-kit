// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactReference behavior and contracts.</summary>
public sealed class ArtifactReferenceTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var reference = Reference();
        reference.Id.ShouldBe(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")));
        reference.Version.ShouldBe(new ArtifactVersion("1"));
        reference.DirectoryId.ShouldBe(new ArtifactDirectoryId("output"));
        reference.ProfileKey.ShouldBe(new ArtifactProfileKey("test"));
        reference.ProfileVersion.ShouldBe(new ArtifactProfileVersion(1));
        reference.TenantId.ShouldBe(Identity().TenantId);
        reference.OwnerId.ShouldBe(new ArtifactOwnerId("session:owner"));
        reference.CreatedBy.ShouldBe(Identity().PrincipalId);
        reference.MediaType.ShouldBe("text/plain");
        reference.Length.ShouldBe(7);
        reference.Integrity.ShouldBe(new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch));
        reference.Classification.ShouldBe(ArtifactDataClassification.Internal);
        reference.Ownership.ShouldBe(ArtifactOwnershipKind.Session);
        reference.Mutability.ShouldBe(ArtifactMutability.Immutable);
        reference.Retention.ShouldBe(new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false));
        reference.CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void ArtifactReference_WhenIdentityIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Reference(id: default(ArtifactId)));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void Constructor_WhenVersionIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), default, new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenDirectoryIdIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), default, new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("directoryId");
    }

    [Fact]
    public void Constructor_WhenProfileKeyIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), default, new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("profileKey");
    }

    [Fact]
    public void Constructor_WhenProfileVersionIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), default, Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("profileVersion");
    }

    [Fact]
    public void Constructor_WhenTenantIdIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), default, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("tenantId");
    }

    [Fact]
    public void Constructor_WhenOwnerIdIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, default, Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("ownerId");
    }

    [Fact]
    public void Constructor_WhenCreatedByIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), default, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("createdBy");
    }

    [Fact]
    public void Constructor_WhenMediaTypeIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, " ", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("mediaType");
    }

    [Fact]
    public void Constructor_WhenLengthIsNegative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", -1, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("length");
    }

    [Fact]
    public void Constructor_WhenIntegrityIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, null!, ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("integrity");
    }

    [Fact]
    public void Constructor_WhenClassificationIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), (ArtifactDataClassification) 999, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("classification");
    }

    [Fact]
    public void Constructor_WhenOwnershipIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, (ArtifactOwnershipKind) 999, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("ownership");
    }

    [Fact]
    public void Constructor_WhenMutabilityIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, (ArtifactMutability) 999, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("mutability");
    }

    [Fact]
    public void Constructor_WhenRetentionIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactReference(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, null!, DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("retention");
    }

    private static ArtifactReference Reference(ArtifactId? id = null, string version = "1") => new(id ?? new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion(version), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Reference();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
