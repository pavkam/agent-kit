// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactMetadata behavior and contracts.</summary>
public sealed class ArtifactMetadataTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var retention = new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false);
        var metadata = new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, retention, null);
        metadata.OwnerId.ShouldBe(new ArtifactOwnerId("session:owner"));
        metadata.MediaType.ShouldBe("text/plain");
        metadata.DeclaredLength.ShouldBe(7);
        metadata.DeclaredContentHash.ShouldBe(new ContentHash("hash"));
        metadata.Classification.ShouldBe(DataClassification.Internal);
        metadata.Ownership.ShouldBe(ArtifactOwnershipKind.Session);
        metadata.Mutability.ShouldBe(ArtifactMutability.Immutable);
        metadata.Retention.ShouldBe(retention);
        metadata.ExternalOwnership.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenNoHashIsDeclared_AllowsNullDeclaredContentHash()
    {
        var metadata = new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, null, DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null);

        metadata.DeclaredContentHash.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenExternallyOwnedWithEvidence_RetainsExternalOwnership()
    {
        var ownership = new ExternalArtifactOwnership(new ExternalArtifactResourceId("vendor:1"), new Uri("https://files.example.com/1"), false);

        var metadata = new ArtifactMetadata(new ArtifactOwnerId("external:owner"), "text/plain", 7, new ContentHash("hash"), DataClassification.Confidential, ArtifactOwnershipKind.External, ArtifactMutability.ExternallyManaged, new ArtifactRetention(new ArtifactRetentionPolicyKey("external"), null, false), ownership);

        metadata.ExternalOwnership.ShouldBe(ownership);
    }

    [Theory]
    [InlineData(ArtifactOwnershipKind.External, ArtifactMutability.ExternallyManaged, false)]
    [InlineData(ArtifactOwnershipKind.External, ArtifactMutability.Immutable, false)]
    [InlineData(ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, true)]
    [InlineData(ArtifactOwnershipKind.Session, ArtifactMutability.ExternallyManaged, false)]
    public void Constructor_WhenExternalOwnershipEvidenceDisagreesWithOwnership_ThrowsExactParameter(
        ArtifactOwnershipKind ownership, ArtifactMutability mutability, bool withEvidence)
    {
        var evidence = withEvidence
            ? new ExternalArtifactOwnership(new ExternalArtifactResourceId("vendor:1"), new Uri("https://files.example.com/1"), false)
            : null;

        var exception = Should.Throw<ArgumentException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), DataClassification.Internal, ownership, mutability, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), evidence));

        exception.ParamName.ShouldBe("externalOwnership");
    }

    [Fact]
    public void ArtifactMetadata_WhenLengthIsNegative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Metadata(declaredLength: -1));
        exception.ParamName.ShouldBe("declaredLength");
    }

    [Fact]
    public void Constructor_WhenOwnerIdIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactMetadata(default, "text/plain", 7, new ContentHash("hash"), DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null));
        exception.ParamName.ShouldBe("ownerId");
    }

    [Fact]
    public void Constructor_WhenMediaTypeIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), " ", 7, new ContentHash("hash"), DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null));
        exception.ParamName.ShouldBe("mediaType");
    }

    [Fact]
    public void Constructor_WhenDeclaredContentHashIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash(), DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null));
        exception.ParamName.ShouldBe("declaredContentHash");
    }

    [Fact]
    public void Constructor_WhenClassificationIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), (DataClassification) 999, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null));
        exception.ParamName.ShouldBe("classification");
    }

    [Fact]
    public void Constructor_WhenOwnershipIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), DataClassification.Internal, (ArtifactOwnershipKind) 999, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null));
        exception.ParamName.ShouldBe("ownership");
    }

    [Fact]
    public void Constructor_WhenMutabilityIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), DataClassification.Internal, ArtifactOwnershipKind.Session, (ArtifactMutability) 999, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null));
        exception.ParamName.ShouldBe("mutability");
    }

    [Fact]
    public void Constructor_WhenRetentionIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, null!, null));
        exception.ParamName.ShouldBe("retention");
    }

    private static ArtifactMetadata Metadata(long declaredLength = 7) => new(new ArtifactOwnerId("session:owner"), "text/plain", declaredLength, new ContentHash("hash"), DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Metadata();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
