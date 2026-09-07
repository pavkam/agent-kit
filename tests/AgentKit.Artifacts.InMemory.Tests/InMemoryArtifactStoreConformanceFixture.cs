// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

/// <summary>Composes an isolated in-memory artifact store and deterministic grant authority for the shared contract suite.</summary>
public sealed class InMemoryArtifactStoreConformanceFixture: IArtifactStoreConformanceFixture
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _clock = new(_now);
    private readonly InMemorySecurityGrantStore _grants;
    private readonly ServiceProvider _services;
    private readonly IArtifactStore _store;
    private int _sequence;

    /// <summary>Initializes isolated store and authority state using one deterministic clock.</summary>
    public InMemoryArtifactStoreConformanceFixture()
    {
        _grants = new(_clock);
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(_clock);
        _ = services.AddSingleton<ISecurityGrantStore>(_grants);
        _ = services.AddInMemoryArtifactStore();
        _services = services.BuildServiceProvider();
        _store = _services.GetRequiredService<IArtifactStore>();
    }

    /// <inheritdoc/>
    public ExecutionIdentity PrimaryIdentity { get; } = CreateIdentity("tenant-a", "principal-a");

    /// <inheritdoc/>
    public ExecutionIdentity SecondaryIdentity { get; } = CreateIdentity("tenant-b", "principal-b");

    /// <inheritdoc/>
    public ValueTask<IArtifactStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_store);
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
        InputFingerprint? grantFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var selectedIdentity = identity ?? PrimaryIdentity;
        var selectedArtifactId = artifactId ?? new ArtifactId(NextGuid());
        var selectedPreparationId = preparationId ?? new ArtifactPreparationId(NextGuid());
        var selectedVersion = version ?? new ArtifactVersion("1");
        var selectedCreatedAt = createdAt ?? _now;
        var selectedExpiresAt = selectedCreatedAt + (lifetime ?? TimeSpan.FromMinutes(5));
        var scope = CreateScope();
        var directory = new ArtifactDirectoryId("conformance");
        var profile = new ArtifactProfileKey("conformance");
        var profileVersion = new ArtifactProfileVersion(1);
        var metadata = new ArtifactMetadata(
            new ArtifactOwnerId("conformance:owner"), "application/octet-stream", content.LongLength,
            FileSecurityBinding.ContentFingerprint(content), ArtifactDataClassification.Internal,
            ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
            new ArtifactRetention(new ArtifactRetentionPolicyKey("conformance"), null, false));
        var fingerprint = grantFingerprint ?? ArtifactSecurityBinding.PrepareFingerprint(
            selectedArtifactId, selectedPreparationId, selectedVersion, profile, profileVersion,
            selectedIdentity.TenantId, selectedIdentity.PrincipalId, directory, metadata,
            selectedCreatedAt, selectedExpiresAt);
        var grant = CreateGrant(
            scope, selectedIdentity, SecurityEffect.Create,
            [ArtifactSecurityBinding.ArtifactResource(selectedArtifactId), ArtifactSecurityBinding.PreparationResource(selectedPreparationId)],
            fingerprint);
        return new(
            selectedArtifactId, selectedPreparationId, selectedVersion, profile, profileVersion,
            selectedIdentity.TenantId, selectedIdentity.PrincipalId, directory, metadata, [.. content],
            selectedCreatedAt, selectedExpiresAt, scope, selectedIdentity, grant, new(idempotencyKey));
    }

    /// <inheritdoc/>
    public ArtifactStoreFinalizeRequest CreateFinalize(ArtifactPreparationId preparationId, ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var scope = CreateScope();
        var grant = CreateGrant(
            scope, identity, SecurityEffect.CreateOrReplace,
            [ArtifactSecurityBinding.PreparationResource(preparationId)],
            ArtifactSecurityBinding.FinalizeFingerprint(preparationId));
        return new(preparationId, scope, identity, grant, new($"finalize-{NextGuid()}"));
    }

    /// <inheritdoc/>
    public ArtifactStoreAbortRequest CreateAbort(ArtifactPreparationId preparationId, ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var scope = CreateScope();
        var grant = CreateGrant(
            scope, identity, SecurityEffect.Delete,
            [ArtifactSecurityBinding.PreparationResource(preparationId)],
            ArtifactSecurityBinding.AbortFingerprint(preparationId, ArtifactAbortReason.Cancelled));
        return new(preparationId, ArtifactAbortReason.Cancelled, scope, identity, grant, new($"abort-{NextGuid()}"));
    }

    /// <inheritdoc/>
    public ArtifactStoreReadRequest CreateRead(ArtifactReference reference, ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(identity);
        var scope = CreateScope();
        var grant = CreateGrant(
            scope, identity, SecurityEffect.Observe, [ArtifactSecurityBinding.ArtifactResource(reference.Id)],
            ArtifactSecurityBinding.ReadFingerprint(reference));
        return new(reference, scope, identity, grant);
    }

    /// <inheritdoc/>
    public ArtifactStoreDeleteRequest CreateDelete(ArtifactReference reference, ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(identity);
        var scope = CreateScope();
        var grant = CreateGrant(
            scope, identity, SecurityEffect.Delete, [ArtifactSecurityBinding.ArtifactResource(reference.Id)],
            ArtifactSecurityBinding.DeleteFingerprint(reference));
        return new(reference, scope, identity, grant, new($"delete-{NextGuid()}"));
    }

    /// <inheritdoc/>
    public ValueTask RegisterGrantAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        return _grants.RegisterAsync(grant, cancellationToken);
    }

    /// <summary>Releases fixture-owned resources; the current in-memory components require no asynchronous cleanup.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => _services.DisposeAsync();

    private SecurityGrant CreateGrant(
        SecurityAuthorizationScope scope,
        ExecutionIdentity identity,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint) => new(
            new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), scope, identity, _store.SecurityAudience,
            SecurityOperationKind.Artifact, effect, resources, fingerprint, new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1), _now, _now.AddHours(1), 1);

    private SecurityAuthorizationScope CreateScope() => new(
        new AgentId(NextGuid()), new SessionId(NextGuid()),
        new InRunOperationCorrelation(new OperationId(NextGuid()), new RunId(NextGuid()), null));

    private static ExecutionIdentity CreateIdentity(string tenant, string principal) =>
        TestSupport.TestExecutionIdentity.Create(
            new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

    private Guid NextGuid()
    {
        _sequence++;
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, _sequence);
        return new(bytes);
    }
}
