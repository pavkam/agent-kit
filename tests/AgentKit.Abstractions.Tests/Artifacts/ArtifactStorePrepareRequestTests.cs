// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactStorePrepareRequest"/> validation and grant-derived evidence.</summary>
public sealed class ArtifactStorePrepareRequestTests
{
    private static readonly ContentHash _hash = new("sha256:content");

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var grant = ArtifactGrant(SecurityEffect.Create);
        var metadata = Metadata();
        var key = new IdempotencyKey("prepare");
        ImmutableArray<byte> content = [1];

        var request = Create(metadata: metadata, content: content, grant: grant, key: key);

        request.ArtifactId.ShouldBe(ArtifactId);
        request.PreparationId.ShouldBe(PreparationId);
        request.Version.ShouldBe(new ArtifactVersion("1"));
        request.ProfileKey.ShouldBe(new ArtifactProfileKey("profile"));
        request.ProfileVersion.ShouldBe(new ArtifactProfileVersion(1));
        request.TenantId.ShouldBe(Identity.TenantId);
        request.CreatedBy.ShouldBe(Identity.PrincipalId);
        request.DirectoryId.ShouldBe(new ArtifactDirectoryId("output"));
        request.Metadata.ShouldBe(metadata);
        request.Content.ShouldBe(content);
        request.ContentHash.ShouldBe(_hash);
        request.CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        request.ExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
        request.Grant.ShouldBe(grant);
        request.Scope.ShouldBe(Scope);
        request.Identity.ShouldBe(Identity);
        request.IdempotencyKey.ShouldBe(key);
    }

    [Fact]
    public void Constructor_WhenStagingDurationIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(expiresAt: DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("expiresAt");
    }

    [Fact]
    public void Constructor_WhenArtifactIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(artifactId: default(ArtifactId)));
        exception.ParamName.ShouldBe("artifactId");
    }

    [Fact]
    public void Constructor_WhenPreparationIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(preparationId: default(ArtifactPreparationId)));
        exception.ParamName.ShouldBe("preparationId");
    }

    [Fact]
    public void Constructor_WhenVersionIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Create(version: default(ArtifactVersion)));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenProfileKeyIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Create(profileKey: default(ArtifactProfileKey)));
        exception.ParamName.ShouldBe("profileKey");
    }

    [Fact]
    public void Constructor_WhenProfileVersionIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(profileVersion: default(ArtifactProfileVersion)));
        exception.ParamName.ShouldBe("profileVersion");
    }

    [Fact]
    public void Constructor_WhenTenantIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Create(tenantId: default(TenantId)));
        exception.ParamName.ShouldBe("tenantId");
    }

    [Fact]
    public void Constructor_WhenCreatorIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Create(createdBy: default(PrincipalId)));
        exception.ParamName.ShouldBe("createdBy");
    }

    [Fact]
    public void Constructor_WhenDirectoryIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Create(directoryId: default(ArtifactDirectoryId)));
        exception.ParamName.ShouldBe("directoryId");
    }

    [Fact]
    public void Constructor_WhenMetadataIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStorePrepareRequest(ArtifactId, PreparationId, new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), Identity.TenantId, Identity.PrincipalId, new ArtifactDirectoryId("output"), null!, [1], _hash, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), ArtifactGrant(SecurityEffect.Create), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("metadata");
    }

    [Fact]
    public void Constructor_WhenContentIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Create(content: default(ImmutableArray<byte>)));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void Constructor_WhenContentHashIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Create(contentHash: default(ContentHash)));
        exception.ParamName.ShouldBe("contentHash");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStorePrepareRequest(ArtifactId, PreparationId, new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), Identity.TenantId, Identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], _hash, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), null!, new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Create(key: default(IdempotencyKey)));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Create();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ArtifactStorePrepareRequest Create(
        ArtifactId? artifactId = null,
        ArtifactPreparationId? preparationId = null,
        ArtifactVersion? version = null,
        ArtifactProfileKey? profileKey = null,
        ArtifactProfileVersion? profileVersion = null,
        TenantId? tenantId = null,
        PrincipalId? createdBy = null,
        ArtifactDirectoryId? directoryId = null,
        ArtifactMetadata? metadata = null,
        ImmutableArray<byte>? content = null,
        ContentHash? contentHash = null,
        DateTimeOffset? expiresAt = null,
        SecurityGrant? grant = null,
        IdempotencyKey? key = null) => new(
            artifactId ?? ArtifactId, preparationId ?? PreparationId, version ?? new ArtifactVersion("1"),
            profileKey ?? new ArtifactProfileKey("profile"), profileVersion ?? new ArtifactProfileVersion(1),
            tenantId ?? Identity.TenantId, createdBy ?? Identity.PrincipalId, directoryId ?? new ArtifactDirectoryId("output"),
            metadata ?? Metadata(), content ?? [1], contentHash ?? _hash, DateTimeOffset.UnixEpoch,
            expiresAt ?? DateTimeOffset.UnixEpoch.AddMinutes(1), grant ?? ArtifactGrant(SecurityEffect.Create),
            key ?? new IdempotencyKey("prepare"));
}
