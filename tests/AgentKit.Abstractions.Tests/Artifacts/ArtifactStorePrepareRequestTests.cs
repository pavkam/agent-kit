// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactStorePrepareRequest behavior and contracts.</summary>
public sealed class ArtifactStorePrepareRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var identity = Identity();
        var scope = Scope();
        var grant = Grant(identity);
        var metadata = Metadata();
        var key = new IdempotencyKey("prepare");
        var artifactId = new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008"));
        var version = new ArtifactVersion("1");
        var profileKey = new ArtifactProfileKey("profile");
        var profileVersion = new ArtifactProfileVersion(1);
        var directoryId = new ArtifactDirectoryId("output");
        ImmutableArray<byte> content = [1];
        var request = new ArtifactStorePrepareRequest(artifactId, PreparationId(), version, profileKey, profileVersion, identity.TenantId, identity.PrincipalId, directoryId, metadata, content, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), scope, identity, grant, key);
        request.ArtifactId.ShouldBe(artifactId);
        request.PreparationId.ShouldBe(PreparationId());
        request.Version.ShouldBe(version);
        request.ProfileKey.ShouldBe(profileKey);
        request.ProfileVersion.ShouldBe(profileVersion);
        request.TenantId.ShouldBe(identity.TenantId);
        request.CreatedBy.ShouldBe(identity.PrincipalId);
        request.DirectoryId.ShouldBe(directoryId);
        request.Metadata.ShouldBe(metadata);
        request.Content.ShouldBe(content);
        request.CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        request.ExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
        request.Scope.ShouldBe(scope);
        request.Identity.ShouldBe(identity);
        request.Grant.ShouldBe(grant);
        request.IdempotencyKey.ShouldBe(key);
    }

    [Fact]
    public void ArtifactStorePrepareRequest_WhenStagingDurationIsNotPositive_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("expiresAt");
    }

    [Fact]
    public void Constructor_WhenArtifactIdIsEmpty_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStorePrepareRequest(default, PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("artifactId");
    }

    [Fact]
    public void Constructor_WhenPreparationIdIsEmpty_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), default, new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("preparationId");
    }

    [Fact]
    public void Constructor_WhenVersionIsBlank_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), default, new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenProfileKeyIsBlank_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), default, new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("profileKey");
    }

    [Fact]
    public void Constructor_WhenProfileVersionIsEmpty_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), default, identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("profileVersion");
    }

    [Fact]
    public void Constructor_WhenTenantIdIsBlank_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), default, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("tenantId");
    }

    [Fact]
    public void Constructor_WhenCreatedByIsBlank_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, default, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("createdBy");
    }

    [Fact]
    public void Constructor_WhenDirectoryIdIsBlank_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, default, Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("directoryId");
    }

    [Fact]
    public void Constructor_WhenMetadataIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), null!, [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("metadata");
    }

    [Fact]
    public void Constructor_WhenContentIsDefault_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), default, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void Constructor_WhenScopeIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), null!, identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), null!, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, null!, new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    private static ArtifactMetadata Metadata(long declaredLength = 7) => new(new ArtifactOwnerId("session:owner"), "text/plain", declaredLength, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false));
    private static SecurityAuthorizationScope Scope() => new(AgentId(), SessionId(), Correlation());
    private static SecurityGrant Grant(ExecutionIdentity identity) => new(new GrantId(Guid.Parse("90000000-0000-0000-0000-000000000009")), new SecurityRequestId(Guid.Parse("a0000000-0000-0000-0000-00000000000a")), Scope(), identity, new ComponentId("artifact-store"), SecurityOperationKind.Artifact, SecurityEffect.Create, [ArtifactSecurityBinding.ArtifactResource(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")))], new InputFingerprint("fingerprint"), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 1);
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static ArtifactPreparationId PreparationId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var identity = Identity();
        var original = new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Scope(), identity, Grant(identity), new IdempotencyKey("prepare"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
