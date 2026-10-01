// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.Permissions.InMemory;

using Microsoft.Extensions.Time.Testing;

/// <summary>Builds deterministic, exactly bound store requests over a fake clock and an in-memory grant authority.</summary>
/// <remarks>
/// A concrete adapter fixture supplies only the store under test. The grant authority is the in-memory security grant store because
/// grants are a separate protected boundary; the store adapter under test still validates and consumes each grant itself.
/// </remarks>
public abstract class ArtifactStoreConformanceFixtureBase: IArtifactStoreConformanceFixture
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private IArtifactStore? _store;
    private int _sequence;

    /// <summary>Initializes isolated clock and authority state.</summary>
    protected ArtifactStoreConformanceFixtureBase()
    {
        Clock = new FakeTimeProvider(_now);
        Grants = new InMemorySecurityGrantStore(Clock);
    }

    /// <inheritdoc/>
    public ExecutionIdentity PrimaryIdentity { get; } = CreateIdentity("tenant-a", "principal-a");

    /// <inheritdoc/>
    public ExecutionIdentity SecondaryIdentity { get; } = CreateIdentity("tenant-b", "principal-b");

    /// <inheritdoc/>
    public DateTimeOffset Now => Clock.GetUtcNow();

    /// <summary>Gets the deterministic clock shared with the store under test.</summary>
    protected FakeTimeProvider Clock { get; }

    /// <summary>Gets the authoritative single-use grant store shared with the store under test.</summary>
    protected InMemorySecurityGrantStore Grants { get; }

    /// <inheritdoc/>
    public void Advance(TimeSpan duration) => Clock.Advance(duration);

    /// <inheritdoc/>
    public ValueTask<IArtifactStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_store ??= CreateStore());
    }

    /// <inheritdoc/>
    public ArtifactMetadata CreateMetadata(
        byte[] content,
        ArtifactRetention? retention = null,
        ExternalArtifactOwnership? externalOwnership = null,
        ContentHash? declaredContentHash = null,
        long? declaredLength = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new ArtifactMetadata(
            new ArtifactOwnerId("conformance:owner"), "application/octet-stream", declaredLength ?? content.LongLength,
            declaredContentHash ?? FileSecurityBinding.ContentFingerprint(content), DataClassification.Internal,
            externalOwnership is null ? ArtifactOwnershipKind.Session : ArtifactOwnershipKind.External,
            externalOwnership is null ? ArtifactMutability.Immutable : ArtifactMutability.ExternallyManaged,
            retention ?? new ArtifactRetention(new ArtifactRetentionPolicyKey("conformance"), null, false), externalOwnership);
    }

    /// <inheritdoc/>
    public ArtifactStorePrepareRequest CreatePrepare(
        byte[] content,
        ExecutionIdentity? identity = null,
        string idempotencyKey = "prepare",
        ArtifactId? artifactId = null,
        ArtifactPreparationId? preparationId = null,
        ArtifactVersion? version = null,
        DateTimeOffset? createdAt = null,
        TimeSpan? lifetime = null,
        InputFingerprint? grantFingerprint = null,
        ArtifactMetadata? metadata = null,
        TenantId? declaredTenant = null,
        PrincipalId? declaredCreator = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var selectedIdentity = identity ?? PrimaryIdentity;
        var selectedArtifactId = artifactId ?? new ArtifactId(NextGuid());
        var selectedPreparationId = preparationId ?? new ArtifactPreparationId(NextGuid());
        var selectedVersion = version ?? new ArtifactVersion("1");
        var selectedCreatedAt = createdAt ?? _now;
        var selectedExpiresAt = selectedCreatedAt + (lifetime ?? TimeSpan.FromMinutes(5));
        var selectedTenant = declaredTenant ?? selectedIdentity.TenantId;
        var selectedCreator = declaredCreator ?? selectedIdentity.PrincipalId;
        var selectedMetadata = metadata ?? CreateMetadata(content);
        var directory = new ArtifactDirectoryId("conformance");
        var profile = new ArtifactProfileKey("conformance");
        var profileVersion = new ArtifactProfileVersion(1);
        var contentHash = FileSecurityBinding.ContentFingerprint(content);
        var fingerprint = grantFingerprint ?? ArtifactSecurityBinding.PrepareFingerprint(
            selectedArtifactId, selectedPreparationId, selectedVersion, profile, profileVersion,
            selectedTenant, selectedCreator, directory, selectedMetadata, contentHash, selectedCreatedAt, selectedExpiresAt);
        var grant = CreateGrant(
            selectedIdentity, SecurityEffect.Create,
            [ArtifactSecurityBinding.ArtifactResource(selectedArtifactId), ArtifactSecurityBinding.PreparationResource(selectedPreparationId)],
            fingerprint);
        return new(
            selectedArtifactId, selectedPreparationId, selectedVersion, profile, profileVersion,
            selectedTenant, selectedCreator, directory, selectedMetadata, [.. content], contentHash,
            selectedCreatedAt, selectedExpiresAt, grant, new(idempotencyKey));
    }

    /// <inheritdoc/>
    public ArtifactStoreFinalizeRequest CreateFinalize(ArtifactPreparationId preparationId, ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var grant = CreateGrant(
            identity, SecurityEffect.CreateOrReplace,
            [ArtifactSecurityBinding.PreparationResource(preparationId)],
            ArtifactSecurityBinding.FinalizeFingerprint(preparationId));
        return new(preparationId, grant, new($"finalize-{NextGuid()}"));
    }

    /// <inheritdoc/>
    public ArtifactStoreAbortRequest CreateAbort(ArtifactPreparationId preparationId, ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var grant = CreateGrant(
            identity, SecurityEffect.Delete,
            [ArtifactSecurityBinding.PreparationResource(preparationId)],
            ArtifactSecurityBinding.AbortFingerprint(preparationId, ArtifactAbortReason.Cancelled));
        return new(preparationId, ArtifactAbortReason.Cancelled, grant, new($"abort-{NextGuid()}"));
    }

    /// <inheritdoc/>
    public ArtifactStoreReadRequest CreateRead(ArtifactReference reference, ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(identity);
        var grant = CreateGrant(
            identity, SecurityEffect.Observe, [ArtifactSecurityBinding.ArtifactResource(reference.Id)],
            ArtifactSecurityBinding.ReadFingerprint(reference));
        return new(reference, grant);
    }

    /// <inheritdoc/>
    public ArtifactStoreDeleteRequest CreateDelete(ArtifactReference reference, ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(identity);
        var grant = CreateGrant(
            identity, SecurityEffect.Delete, [ArtifactSecurityBinding.ArtifactResource(reference.Id)],
            ArtifactSecurityBinding.DeleteFingerprint(reference));
        return new(reference, grant, new($"delete-{NextGuid()}"));
    }

    /// <inheritdoc/>
    public ValueTask RegisterGrantAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        return Grants.RegisterAsync(grant, cancellationToken);
    }

    /// <summary>Releases adapter-owned resources created by <see cref="CreateStore"/>.</summary>
    /// <returns>A task representing cleanup.</returns>
    public abstract ValueTask DisposeAsync();

    /// <summary>Creates the store under test over <see cref="Grants"/> and <see cref="Clock"/>, exactly once per fixture.</summary>
    /// <returns>A store owned by this fixture.</returns>
    protected abstract IArtifactStore CreateStore();

    /// <summary>Creates a deterministic unique identifier for request reservation, grant, and scope identities.</summary>
    /// <returns>A non-empty GUID derived from a per-fixture counter.</returns>
    protected Guid NextGuid()
    {
        var sequence = Interlocked.Increment(ref _sequence);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, sequence);
        return new(bytes);
    }

    private SecurityGrant CreateGrant(
        ExecutionIdentity identity,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint)
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(NextGuid()), new SessionId(NextGuid()),
            new InRunOperationCorrelation(new OperationId(NextGuid()), new RunId(NextGuid()), null));
        var authorization = TestSupport.TestSecurityEvidence.Authorization(scope.AgentId, scope.SessionId, scope.Correlation, identity);
        return new SecurityGrant(
            new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), scope, identity, authorization,
            (_store ??= CreateStore()).SecurityAudience, SecurityOperationKind.Artifact, effect, resources, fingerprint,
            new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), _now, _now.AddHours(1), 1);
    }

    private static ExecutionIdentity CreateIdentity(string tenant, string principal) =>
        TestSupport.TestExecutionIdentity.Create(
            new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);
}
