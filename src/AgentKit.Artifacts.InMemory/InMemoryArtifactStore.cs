// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

/// <summary>Stores staged and committed artifact bytes in tenant-partitioned process memory for deterministic tests.</summary>
/// <remarks>Content survives only for the lifetime of this store. Staging is never readable as committed content, and all state access follows exact single-use grant consumption.</remarks>
public sealed class InMemoryArtifactStore: IArtifactStore
{
    private readonly Lock _lock = new();
    private readonly ISecurityGrantStore _grants;
    private readonly TimeProvider _time;
    private readonly Dictionary<ArtifactPreparationId, PreparedState> _prepared = [];
    private readonly Dictionary<ArtifactPreparationId, FinalizedState> _finalized = [];
    private readonly Dictionary<ArtifactKey, FinalizedState> _committed = [];
    private readonly HashSet<ArtifactPreparationId> _aborted = [];
    private readonly HashSet<ArtifactKey> _deleted = [];
    private readonly Dictionary<ReplayKey, PreparedState> _prepareReplay = [];

    /// <summary>Initializes the store over the authoritative grant store and deterministic clock.</summary>
    /// <param name="grants">The atomic single-use grant store.</param>
    /// <param name="time">The clock used for expiry and publication evidence.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public InMemoryArtifactStore(ISecurityGrantStore grants, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(time);
        _grants = grants;
        _time = time;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.artifacts.in-memory");

    /// <inheritdoc/>
    public async ValueTask<ArtifactPrepareResult> PrepareAsync(ArtifactStorePrepareRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var consumed = await ConsumeAsync(
            request.Grant, request.Scope, request.Identity, SecurityEffect.Create,
            [ArtifactSecurityBinding.ArtifactResource(request.ArtifactId), ArtifactSecurityBinding.PreparationResource(request.PreparationId)],
            ArtifactSecurityBinding.PrepareFingerprint(request.ArtifactId, request.PreparationId, request.DirectoryId, request.Metadata), cancellationToken).ConfigureAwait(false);
        if (consumed is not null)
        {
            return new ArtifactPrepareRejected(consumed);
        }

        var observedHash = FileSecurityBinding.ContentFingerprint(request.Content.AsSpan());
        if (request.Content.Length != request.Metadata.DeclaredLength || observedHash != request.Metadata.DeclaredContentHash)
        {
            return RejectPrepare(ArtifactFailureKind.IntegrityMismatch, "Backend integrity validation did not match the declaration.");
        }

        var snapshot = new PreparedSnapshot(
            request.ArtifactId,
            request.PreparationId,
            request.Version,
            request.ProfileKey,
            request.ProfileVersion,
            request.TenantId,
            request.CreatedBy,
            request.DirectoryId,
            request.Metadata,
            request.Content,
            request.CreatedAt,
            request.ExpiresAt);
        var state = new PreparedState(
            snapshot,
            new ArtifactPrepared(request.PreparationId, request.ArtifactId, request.Version, request.ExpiresAt));
        var replay = new ReplayKey(request.TenantId, "prepare", request.IdempotencyKey.Value);
        lock (_lock)
        {
            if (_prepareReplay.TryGetValue(replay, out var prior))
            {
                return Equivalent(prior.Snapshot, request)
                    ? prior.Receipt
                    : RejectPrepare(ArtifactFailureKind.Conflict, "The prepare idempotency key was reused with different content or policy.");
            }

            if (_prepared.ContainsKey(request.PreparationId) || _finalized.ContainsKey(request.PreparationId) || _aborted.Contains(request.PreparationId))
            {
                return RejectPrepare(ArtifactFailureKind.Conflict, "The preparation identity is already in use.");
            }

            _prepared.Add(request.PreparationId, state);
            _prepareReplay.Add(replay, state);
            return state.Receipt;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactStoreFinalizeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var consumed = await ConsumeAsync(
            request.Grant, request.Scope, request.Identity, SecurityEffect.CreateOrReplace,
            [ArtifactSecurityBinding.PreparationResource(request.PreparationId)],
            ArtifactSecurityBinding.FinalizeFingerprint(request.PreparationId), cancellationToken).ConfigureAwait(false);
        if (consumed is not null)
        {
            return new ArtifactFinalizeRejected(consumed);
        }

        lock (_lock)
        {
            if (_finalized.TryGetValue(request.PreparationId, out var prior))
            {
                var priorKey = new ArtifactKey(prior.Result.Reference.Id, prior.Result.Reference.Version);
                return prior.Snapshot.TenantId == request.Identity.TenantId && !_deleted.Contains(priorKey)
                    ? prior.Result
                    : RejectFinalize(ArtifactFailureKind.NotFound, "The preparation is unavailable or belongs to another tenant.");
            }

            if (!_prepared.TryGetValue(request.PreparationId, out var staged) || staged.Snapshot.TenantId != request.Identity.TenantId)
            {
                return RejectFinalize(ArtifactFailureKind.NotFound, "The preparation is unavailable or belongs to another tenant.");
            }

            if (staged.Snapshot.ExpiresAt <= _time.GetUtcNow())
            {
                _ = _prepared.Remove(request.PreparationId);
                _ = _aborted.Add(request.PreparationId);
                return RejectFinalize(ArtifactFailureKind.NotFound, "The preparation expired before publication.");
            }

            var publicationTime = _time.GetUtcNow();
            var snapshot = staged.Snapshot;
            var metadata = snapshot.Metadata;
            var reference = new ArtifactReference(
                snapshot.ArtifactId, snapshot.Version, snapshot.DirectoryId,
                snapshot.ProfileKey, snapshot.ProfileVersion, snapshot.TenantId,
                metadata.OwnerId, snapshot.CreatedBy, metadata.MediaType, metadata.DeclaredLength,
                new ArtifactIntegrity(metadata.DeclaredContentHash, publicationTime), metadata.Classification,
                metadata.Ownership, metadata.Mutability, metadata.Retention, publicationTime);
            var result = new ArtifactFinalized(reference);
            _ = _prepared.Remove(request.PreparationId);
            var finalized = new FinalizedState(snapshot, result);
            _finalized.Add(request.PreparationId, finalized);
            _committed.Add(new ArtifactKey(reference.Id, reference.Version), finalized);
            return result;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactAbortResult> AbortAsync(ArtifactStoreAbortRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var consumed = await ConsumeAsync(
            request.Grant, request.Scope, request.Identity, SecurityEffect.Delete,
            [ArtifactSecurityBinding.PreparationResource(request.PreparationId)],
            ArtifactSecurityBinding.AbortFingerprint(request.PreparationId, request.Reason), cancellationToken).ConfigureAwait(false);
        if (consumed is not null)
        {
            return new ArtifactAbortRejected(consumed);
        }

        lock (_lock)
        {
            if (_finalized.TryGetValue(request.PreparationId, out var finalized))
            {
                return _deleted.Contains(new ArtifactKey(finalized.Result.Reference.Id, finalized.Result.Reference.Version))
                    ? new ArtifactAborted(true)
                    : finalized.Snapshot.TenantId == request.Identity.TenantId
                    ? new ArtifactAbortRejected(new ArtifactFailure(ArtifactFailureKind.Conflict, "Committed artifact content cannot be aborted."))
                    : new ArtifactAbortRejected(new ArtifactFailure(ArtifactFailureKind.NotFound, "The preparation is unavailable or belongs to another tenant."));
            }

            if (_prepared.TryGetValue(request.PreparationId, out var staged) && staged.Snapshot.TenantId != request.Identity.TenantId)
            {
                return new ArtifactAbortRejected(new ArtifactFailure(ArtifactFailureKind.NotFound, "The preparation is unavailable or belongs to another tenant."));
            }

            var removed = _prepared.Remove(request.PreparationId);
            _ = _aborted.Add(request.PreparationId);
            return new ArtifactAborted(!removed);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactReadResult> ReadAsync(ArtifactStoreReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var consumed = await ConsumeAsync(
            request.Grant, request.Scope, request.Identity, SecurityEffect.Observe,
            [ArtifactSecurityBinding.ArtifactResource(request.Reference.Id)],
            ArtifactSecurityBinding.ReadFingerprint(request.Reference), cancellationToken).ConfigureAwait(false);
        if (consumed is not null)
        {
            return new ArtifactReadRejected(consumed);
        }

        lock (_lock)
        {
            if (request.Reference.TenantId != request.Identity.TenantId)
            {
                return RejectRead(ArtifactFailureKind.NotFound, "The committed artifact is unavailable or belongs to another tenant.");
            }

            var key = new ArtifactKey(request.Reference.Id, request.Reference.Version);
            return !_committed.TryGetValue(key, out var committed) || committed.Result.Reference != request.Reference
                ? RejectRead(ArtifactFailureKind.NotFound, "The exact committed artifact version was not found.")
                : new ArtifactReadOpened(
                committed.Result.Reference,
                new MemoryStream(committed.Snapshot.Content.ToArray(), writable: false));
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactStoreDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var consumed = await ConsumeAsync(
            request.Grant, request.Scope, request.Identity, SecurityEffect.Delete,
            [ArtifactSecurityBinding.ArtifactResource(request.Reference.Id)],
            ArtifactSecurityBinding.DeleteFingerprint(request.Reference), cancellationToken).ConfigureAwait(false);
        if (consumed is not null)
        {
            return new ArtifactDeleteRejected(consumed);
        }

        lock (_lock)
        {
            if (request.Reference.TenantId != request.Identity.TenantId)
            {
                return RejectDelete(ArtifactFailureKind.NotFound, "The committed artifact is unavailable or belongs to another tenant.");
            }

            var key = new ArtifactKey(request.Reference.Id, request.Reference.Version);
            if (_deleted.Contains(key))
            {
                return new ArtifactDeleted(true);
            }

            if (!_committed.TryGetValue(key, out var committed) || committed.Result.Reference != request.Reference)
            {
                return RejectDelete(ArtifactFailureKind.NotFound, "The exact committed artifact version was not found.");
            }

            if (committed.Result.Reference.Retention.LegalHold)
            {
                return RejectDelete(ArtifactFailureKind.RetentionConflict, "Artifact retention prohibits deletion.");
            }

            _ = _committed.Remove(key);
            _ = _deleted.Add(key);
            return new ArtifactDeleted(false);
        }
    }

    private async ValueTask<ArtifactFailure?> ConsumeAsync(SecurityGrant grant, SecurityAuthorizationScope scope, ExecutionIdentity identity, SecurityEffect effect, ImmutableArray<ProtectedResource> resources, InputFingerprint fingerprint, CancellationToken cancellationToken)
    {
        var result = await _grants.ValidateAndConsumeAsync(grant, new SecurityEnforcementRequest(
            scope, identity, SecurityAudience, SecurityOperationKind.Artifact, effect, resources, fingerprint, grant.RevocationVersion), cancellationToken).ConfigureAwait(false);
        return result.Status == GrantConsumptionStatus.Consumed
            ? null
            : new ArtifactFailure(ArtifactFailureKind.Denied, result.SafeMessage);
    }

    private static bool Equivalent(PreparedSnapshot left, ArtifactStorePrepareRequest right) =>
        left.DirectoryId == right.DirectoryId && left.Metadata == right.Metadata && left.Content.SequenceEqual(right.Content)
        && left.ProfileKey == right.ProfileKey && left.ProfileVersion == right.ProfileVersion
        && left.TenantId == right.TenantId && left.CreatedBy == right.CreatedBy;

    private static ArtifactPrepareRejected RejectPrepare(ArtifactFailureKind kind, string message) => new(new ArtifactFailure(kind, message));
    private static ArtifactFinalizeRejected RejectFinalize(ArtifactFailureKind kind, string message) => new(new ArtifactFailure(kind, message));
    private static ArtifactReadRejected RejectRead(ArtifactFailureKind kind, string message) => new(new ArtifactFailure(kind, message));
    private static ArtifactDeleteRejected RejectDelete(ArtifactFailureKind kind, string message) => new(new ArtifactFailure(kind, message));
    private sealed record PreparedState(PreparedSnapshot Snapshot, ArtifactPrepared Receipt);
    private sealed record PreparedSnapshot(
        ArtifactId ArtifactId,
        ArtifactPreparationId PreparationId,
        ArtifactVersion Version,
        ArtifactProfileKey ProfileKey,
        ArtifactProfileVersion ProfileVersion,
        TenantId TenantId,
        PrincipalId CreatedBy,
        ArtifactDirectoryId DirectoryId,
        ArtifactMetadata Metadata,
        ImmutableArray<byte> Content,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt);
    private sealed record FinalizedState(PreparedSnapshot Snapshot, ArtifactFinalized Result);
    private readonly record struct ArtifactKey(ArtifactId Id, ArtifactVersion Version);
    private readonly record struct ReplayKey(TenantId TenantId, string Operation, string Key);
}
