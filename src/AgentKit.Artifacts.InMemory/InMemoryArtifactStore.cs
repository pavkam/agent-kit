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
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly Dictionary<TenantArtifactPreparationKey, PreparedState> _prepared = [];
    private readonly Dictionary<TenantArtifactPreparationKey, FinalizedState> _finalized = [];
    private readonly Dictionary<TenantArtifactKey, FinalizedState> _committed = [];
    private readonly HashSet<TenantArtifactPreparationKey> _aborted = [];
    private readonly Dictionary<TenantArtifactKey, ArtifactReference> _deleted = [];
    private readonly Dictionary<ReplayKey, PreparedState> _prepareReplay = [];

    /// <summary>Initializes the store over the authoritative grant store and deterministic clock.</summary>
    /// <param name="grants">The atomic single-use grant store.</param>
    /// <param name="time">The clock used for expiry and publication evidence.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public InMemoryArtifactStore(ISecurityGrantStore grants, TimeProvider time)
        : this(grants, time, new GuidSecurityEnforcementIntentIdGenerator())
    {
    }

    /// <summary>Initializes the store with a replaceable source of fresh atomic enforcement-intent identities.</summary>
    /// <param name="grants">The non-null authoritative store that atomically consumes a grant and retains permission to begin.</param>
    /// <param name="time">The non-null clock used for expiry and publication evidence.</param>
    /// <param name="intentIds">The non-null thread-safe source of unique per-operation enforcement intent identities.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grants"/>, <paramref name="time"/>, or <paramref name="intentIds"/> is null.</exception>
    public InMemoryArtifactStore(
        ISecurityGrantStore grants,
        TimeProvider time,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(intentIds);
        _grants = grants;
        _time = time;
        _intentIds = intentIds;
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
            ArtifactSecurityBinding.PrepareFingerprint(
                request.ArtifactId, request.PreparationId, request.Version, request.ProfileKey,
                request.ProfileVersion, request.TenantId, request.CreatedBy, request.DirectoryId,
                request.Metadata, request.CreatedAt, request.ExpiresAt), cancellationToken).ConfigureAwait(false);
        if (consumed is not null)
        {
            return new ArtifactPrepareRejected(consumed);
        }

        if (request.TenantId != request.Identity.TenantId)
        {
            return RejectPrepare(ArtifactFailureKind.Denied, "The declared tenant does not match the authenticated identity.");
        }

        if (request.CreatedBy != request.Identity.PrincipalId)
        {
            return RejectPrepare(ArtifactFailureKind.Denied, "The declared creator does not match the authenticated identity.");
        }

        // A non-positive staging lifetime cannot reach this method: ArtifactStorePrepareRequest's own
        // constructor already rejects ExpiresAt <= CreatedAt, so every valid request instance guarantees
        // a positive staging lifetime by construction.
        var observedHash = FileSecurityBinding.ContentFingerprint(request.Content.AsSpan());
        if (request.Content.Length != request.Metadata.DeclaredLength || observedHash != request.Metadata.DeclaredContentHash)
        {
            return RejectPrepare(ArtifactFailureKind.IntegrityMismatch, "Backend integrity validation did not match the declaration.");
        }

        var snapshot = new PreparedSnapshot(
            request.ArtifactId,
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

            var preparationKey = new TenantArtifactPreparationKey(request.Identity.TenantId, request.PreparationId);
            if (_prepared.ContainsKey(preparationKey) || _finalized.ContainsKey(preparationKey) || _aborted.Contains(preparationKey))
            {
                return RejectPrepare(ArtifactFailureKind.Conflict, "The preparation identity is already in use.");
            }

            _prepared.Add(preparationKey, state);
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
            var preparationKey = new TenantArtifactPreparationKey(request.Identity.TenantId, request.PreparationId);
            if (_finalized.TryGetValue(preparationKey, out var prior))
            {
                var priorKey = new TenantArtifactKey(request.Identity.TenantId, prior.Result.Reference.Id, prior.Result.Reference.Version);
                return !_deleted.ContainsKey(priorKey)
                    ? prior.Result
                    : RejectFinalize(ArtifactFailureKind.NotFound, "The preparation is unavailable or belongs to another tenant.");
            }

            if (!_prepared.TryGetValue(preparationKey, out var staged))
            {
                return RejectFinalize(ArtifactFailureKind.NotFound, "The preparation is unavailable or belongs to another tenant.");
            }

            if (staged.Snapshot.ExpiresAt <= _time.GetUtcNow())
            {
                _ = _prepared.Remove(preparationKey);
                _ = _aborted.Add(preparationKey);
                return RejectFinalize(ArtifactFailureKind.NotFound, "The preparation expired before publication.");
            }

            var snapshot = staged.Snapshot;
            var artifactKey = new TenantArtifactKey(request.Identity.TenantId, snapshot.ArtifactId, snapshot.Version);
            if (_deleted.TryGetValue(artifactKey, out var deletedReference))
            {
                return deletedReference.TenantId == snapshot.TenantId
                    ? RejectFinalize(ArtifactFailureKind.Conflict, "A deleted immutable artifact version cannot be published again.")
                    : RejectFinalize(ArtifactFailureKind.NotFound, "The artifact identity is unavailable or belongs to another tenant.");
            }

            if (_committed.TryGetValue(artifactKey, out var existing))
            {
                return existing.Snapshot.TenantId == snapshot.TenantId
                    ? RejectFinalize(ArtifactFailureKind.Conflict, "The immutable artifact version is already committed.")
                    : RejectFinalize(ArtifactFailureKind.NotFound, "The artifact identity is unavailable or belongs to another tenant.");
            }

            var publicationTime = _time.GetUtcNow();
            var metadata = snapshot.Metadata;
            var reference = new ArtifactReference(
                snapshot.ArtifactId, snapshot.Version, snapshot.DirectoryId,
                snapshot.ProfileKey, snapshot.ProfileVersion, snapshot.TenantId,
                metadata.OwnerId, snapshot.CreatedBy, metadata.MediaType, metadata.DeclaredLength,
                new ArtifactIntegrity(metadata.DeclaredContentHash, publicationTime), metadata.Classification,
                metadata.Ownership, metadata.Mutability, metadata.Retention, publicationTime);
            var result = new ArtifactFinalized(reference);
            _ = _prepared.Remove(preparationKey);
            var finalized = new FinalizedState(snapshot, result);
            _finalized.Add(preparationKey, finalized);
            _committed.Add(artifactKey, finalized);
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
            var preparationKey = new TenantArtifactPreparationKey(request.Identity.TenantId, request.PreparationId);
            if (_finalized.TryGetValue(preparationKey, out var finalized))
            {
                return _deleted.ContainsKey(new TenantArtifactKey(request.Identity.TenantId, finalized.Result.Reference.Id, finalized.Result.Reference.Version))
                    ? new ArtifactAborted(true)
                    : new ArtifactAbortRejected(new ArtifactFailure(ArtifactFailureKind.Conflict, "Committed artifact content cannot be aborted."));
            }

            if (_aborted.Contains(preparationKey))
            {
                return new ArtifactAborted(true);
            }

            var removed = _prepared.Remove(preparationKey);
            if (removed)
            {
                _ = _aborted.Add(preparationKey);
                return new ArtifactAborted(false);
            }

            return new ArtifactAbortRejected(new ArtifactFailure(ArtifactFailureKind.NotFound, "The preparation is unavailable or belongs to another tenant."));
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

            var key = new TenantArtifactKey(request.Identity.TenantId, request.Reference.Id, request.Reference.Version);
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

            var key = new TenantArtifactKey(request.Identity.TenantId, request.Reference.Id, request.Reference.Version);
            if (_deleted.TryGetValue(key, out var deletedReference))
            {
                return deletedReference == request.Reference
                    ? new ArtifactDeleted(true)
                    : RejectDelete(ArtifactFailureKind.NotFound, "The exact committed artifact version was not found.");
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
            _deleted.Add(key, committed.Result.Reference);
            return new ArtifactDeleted(false);
        }
    }

    private async ValueTask<ArtifactFailure?> ConsumeAsync(SecurityGrant grant, SecurityAuthorizationScope scope, ExecutionIdentity identity, SecurityEffect effect, ImmutableArray<ProtectedResource> resources, InputFingerprint fingerprint, CancellationToken cancellationToken)
    {
        if (grant.Authorization is { } authorization
            && (authorization.Scope != scope || authorization.Identity != identity))
        {
            return new ArtifactFailure(
                ArtifactFailureKind.Denied,
                "The captured authorization does not match the artifact operation.");
        }

        var enforcement = ArtifactEnforcementReceipt.Create(
            grant,
            scope,
            identity,
            SecurityAudience,
            effect,
            resources,
            fingerprint);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var result = await _grants.ValidateAndConsumeAsync(
            grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return ArtifactEnforcementReceipt.IsFreshExact(result, grant, enforcement, intent)
            ? null
            : new ArtifactFailure(ArtifactFailureKind.Denied, ArtifactEnforcementReceipt.DenialMessage(result));
    }

    private static bool Equivalent(PreparedSnapshot left, ArtifactStorePrepareRequest right) =>
        left.DirectoryId == right.DirectoryId && left.Metadata == right.Metadata && left.Content.SequenceEqual(right.Content)
        && left.Version == right.Version && left.ProfileKey == right.ProfileKey && left.ProfileVersion == right.ProfileVersion
        && left.TenantId == right.Identity.TenantId && left.CreatedBy == right.Identity.PrincipalId
        && left.ExpiresAt - left.CreatedAt == right.ExpiresAt - right.CreatedAt;

    private static ArtifactPrepareRejected RejectPrepare(ArtifactFailureKind kind, string message) => new(new ArtifactFailure(kind, message));
    private static ArtifactFinalizeRejected RejectFinalize(ArtifactFailureKind kind, string message) => new(new ArtifactFailure(kind, message));
    private static ArtifactReadRejected RejectRead(ArtifactFailureKind kind, string message) => new(new ArtifactFailure(kind, message));
    private static ArtifactDeleteRejected RejectDelete(ArtifactFailureKind kind, string message) => new(new ArtifactFailure(kind, message));
    private sealed record PreparedState(PreparedSnapshot Snapshot, ArtifactPrepared Receipt);
    private sealed record PreparedSnapshot(
        ArtifactId ArtifactId,
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
}
