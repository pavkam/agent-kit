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
        var metadata = new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, retention);
        metadata.OwnerId.ShouldBe(new ArtifactOwnerId("session:owner"));
        metadata.MediaType.ShouldBe("text/plain");
        metadata.DeclaredLength.ShouldBe(7);
        metadata.DeclaredContentHash.ShouldBe(new ContentHash("hash"));
        metadata.Classification.ShouldBe(ArtifactDataClassification.Internal);
        metadata.Ownership.ShouldBe(ArtifactOwnershipKind.Session);
        metadata.Mutability.ShouldBe(ArtifactMutability.Immutable);
        metadata.Retention.ShouldBe(retention);
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
        var exception = Should.Throw<ArgumentException>(() => new ArtifactMetadata(default, "text/plain", 7, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false)));
        exception.ParamName.ShouldBe("ownerId");
    }

    [Fact]
    public void Constructor_WhenMediaTypeIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), " ", 7, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false)));
        exception.ParamName.ShouldBe("mediaType");
    }

    [Fact]
    public void Constructor_WhenDeclaredContentHashIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, default, ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false)));
        exception.ParamName.ShouldBe("declaredContentHash");
    }

    [Fact]
    public void Constructor_WhenClassificationIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), (ArtifactDataClassification) 999, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false)));
        exception.ParamName.ShouldBe("classification");
    }

    [Fact]
    public void Constructor_WhenOwnershipIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), ArtifactDataClassification.Internal, (ArtifactOwnershipKind) 999, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false)));
        exception.ParamName.ShouldBe("ownership");
    }

    [Fact]
    public void Constructor_WhenMutabilityIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, (ArtifactMutability) 999, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false)));
        exception.ParamName.ShouldBe("mutability");
    }

    [Fact]
    public void Constructor_WhenRetentionIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", 7, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, null!));
        exception.ParamName.ShouldBe("retention");
    }

    private static ArtifactMetadata Metadata(long declaredLength = 7) => new(new ArtifactOwnerId("session:owner"), "text/plain", declaredLength, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Metadata();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
