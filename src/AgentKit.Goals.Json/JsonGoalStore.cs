// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Json;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores goals, attempts, and transitions as a flushed newline-delimited JSON log under one fixed local root.</summary>
/// <remarks>
/// <para>
/// Every acknowledged write appends one full goal snapshot and is flushed to disk before the call returns, so an
/// acknowledged record survives process loss. Live state is projected during initialization by replaying the log through the
/// same shared state machine the in-memory adapter runs, and a torn trailing append is recovered or refused according to the
/// target's recovery mode. The captured authorization of a delegated child is persisted, so a restarted worker discovers
/// open intents through <see cref="ReadIntentsAsync"/> and selects the same authority the delegation was made under.
/// </para>
/// <para>
/// The adapter holds an advisory exclusive lock on its root and rejects a second writer, so it claims no multi-process
/// coordination. Every operation consumes a single-use grant that binds that exact operation before any state is read or
/// written. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class JsonGoalStore: IGoalStore, IDisposable
{
    private const string _adapter = "json";
    private const string _storeKind = "agentkit.goals";
    private const string _logName = "goals";
    private const int _schemaVersion = 1;

    private readonly JsonGoalStoreTarget _target;
    private readonly JsonGoalStoreSettings _settings;
    private readonly GoalStoreEnforcement _enforcement;
    private readonly HashSet<ComponentId> _scanners;
    private readonly TimeProvider _time;
    private readonly ILogger<JsonGoalStore> _logger;
    private readonly JsonStoreRoot _root;
    private readonly JsonRecordLog _log;
    private readonly GoalStoreState _state = new();
    private readonly Lock _gate = new();
    private JsonStoreLock? _exclusive;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes a store bound to one host-authorized root without opening, creating, or locking it.</summary>
    /// <param name="target">The non-null exact root and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable bounds and encoding contract.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public JsonGoalStore(
        JsonGoalStoreTarget target,
        JsonGoalStoreSettings settings,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<JsonGoalStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        Descriptor = new GoalStoreDescriptor("agentkit.goals.json", new ComponentId("agentkit.goals.json"), isDurable: true, supportsIntentDiscovery: true);
        _target = target;
        _settings = settings;
        _enforcement = new GoalStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _scanners = [.. settings.AuthorizedIntentScanners];
        _time = time;
        _logger = logger ?? NullLogger<JsonGoalStore>.Instance;
        _root = new JsonStoreRoot(target.DirectoryPath);
        _log = new JsonRecordLog(_root.LogPath(_logName), settings.MaximumRecordBytes);
    }

    /// <inheritdoc/>
    public GoalStoreDescriptor Descriptor { get; }

    /// <summary>Validates or creates the root, binds its encoding contract, and replays recorded snapshots into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, identity, encoding contract, or persisted evidence cannot be validated safely, or a second writer holds the advisory lock.</exception>
    /// <exception cref="ObjectDisposedException">The store was disposed.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Calling it during trusted host startup surfaces a misconfigured root at boot instead of at first use. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            EnsureInitialized(cancellationToken);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<GoalCreateResult> CreateAsync(GoalCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync<GoalCreateResult>(
            _logger, _time, _adapter, GoalStoreOperationKind.Create, request.Goal.Id, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                    [GoalSecurityBinding.Resource(request.Goal.Id)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalCreateRejected(denial);
                }

                if (GoalStoreEnforcement.CheckOwner(request.Grant, request.Goal.OwnerAgentId, request.Goal.SessionId) is { } mismatch)
                {
                    return new GoalCreateRejected(mismatch);
                }

                lock (_gate)
                {
                    EnsureInitialized(cancellationToken);
                    var plan = _state.PlanCreate(request.Grant.Identity.TenantId, request);
                    var failure = Persist(plan, cancellationToken);
                    return failure is not null
                        ? new GoalCreateRejected(failure)
                        : new GoalCreated(plan.Record!, replayed: plan.Kind == GoalReductionKind.Replayed);
                }
            },
            static result => (result as GoalCreateRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalLoadResult> LoadAsync(GoalLoadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync<GoalLoadResult>(
            _logger, _time, _adapter, GoalStoreOperationKind.Load, request.GoalId, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    [GoalSecurityBinding.Resource(request.GoalId)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalLoadRejected(denial);
                }

                GoalRecord? record;
                lock (_gate)
                {
                    EnsureInitialized(cancellationToken);
                    record = _state.Find(request.Grant.Identity.TenantId, request.GoalId);
                }

                return record is null
                    ? new GoalLoadRejected(new GoalStoreFailure(GoalStoreFailureKind.NotFound, "The goal does not exist."))
                    : GoalStoreEnforcement.CheckOwner(request.Grant, record.Goal.OwnerAgentId, record.Goal.SessionId) is { } mismatch
                        ? new GoalLoadRejected(mismatch)
                        : new GoalLoaded(record);
            },
            static result => (result as GoalLoadRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalTransitionResult> TransitionAsync(GoalTransitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync<GoalTransitionResult>(
            _logger, _time, _adapter, GoalStoreOperationKind.Transition, request.Transition.GoalId, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                    [GoalSecurityBinding.Resource(request.Transition.GoalId)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalTransitionRejected(denial);
                }

                if (GoalStoreEnforcement.CheckOwner(request.Grant, request.Transition.OwnerAgentId, request.Transition.SessionId) is { } mismatch)
                {
                    return new GoalTransitionRejected(mismatch);
                }

                lock (_gate)
                {
                    EnsureInitialized(cancellationToken);
                    var plan = _state.PlanTransition(request.Grant.Identity.TenantId, request);
                    var failure = Persist(plan, cancellationToken);
                    return failure is not null
                        ? new GoalTransitionRejected(failure)
                        : new GoalTransitioned(plan.Record!, replayed: plan.Kind == GoalReductionKind.Replayed);
                }
            },
            static result => (result as GoalTransitionRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalPageResult> ReadChildrenAsync(GoalChildrenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync(
            _logger, _time, _adapter, GoalStoreOperationKind.ReadChildren, request.ParentId, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    [GoalSecurityBinding.ChildrenResource(request.ParentId)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalPageRejected(denial);
                }

                lock (_gate)
                {
                    EnsureInitialized(cancellationToken);
                    return _state.ReadChildren(request.Grant.Identity.TenantId, request);
                }
            },
            static result => (result as GoalPageRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalPageResult> ReadIntentsAsync(GoalIntentScanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync(
            _logger, _time, _adapter, GoalStoreOperationKind.ReadIntents, null, null,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_scanners.Contains(request.Scanner))
                {
                    return ValueTask.FromResult<GoalPageResult>(new GoalPageRejected(
                        new GoalStoreFailure(GoalStoreFailureKind.Denied, "The scanner is not configured for this store.")));
                }

                lock (_gate)
                {
                    EnsureInitialized(cancellationToken);
                    return ValueTask.FromResult(_state.ReadIntents(request));
                }
            },
            static result => (result as GoalPageRejected)?.Failure);
    }

    /// <summary>Releases the advisory lock so another writer may open the root.</summary>
    /// <remarks>Disposal is idempotent. Acknowledged records were already flushed and are unaffected.</remarks>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _exclusive?.Dispose();
            _exclusive = null;
        }
    }

    private static GoalStoreFailure Unwritable() =>
        new(GoalStoreFailureKind.Unavailable, "The goal could not be written durably; its commit status is unknown.");

    private GoalStoreFailure? Persist(GoalPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Kind == GoalReductionKind.Rejected)
        {
            return plan.Failure;
        }

        if (plan.Kind == GoalReductionKind.Replayed)
        {
            return null;
        }

        try
        {
            _log.Append(
                JsonStoreSerialization.Encode(
                    PersistedGoalDocument.FromDomain(plan.Tenant, plan.CreateKey, plan.Record!),
                    _settings.Encoding.RecordOptions,
                    _settings.MaximumRecordBytes),
                cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return Unwritable();
        }

        _state.Commit(plan);
        return null;
    }

    private void EnsureInitialized(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        _root.Validate(allowCreate: _target.OpenMode == JsonStoreOpenMode.CreateIfMissing);
        JsonStoreRoot.ValidateFile(_root.ManifestPath);
        JsonStoreRoot.ValidateFile(_log.Path);
        _exclusive = JsonStoreLock.Acquire(_root.LockPath);
        try
        {
            VerifyEncoding();
            BindManifest(cancellationToken);
            Replay(cancellationToken);
            _initialized = true;
        }
        catch
        {
            _exclusive.Dispose();
            _exclusive = null;
            throw;
        }
    }

    private void VerifyEncoding()
    {
        var (probeTenant, probeKey, probeRecord) = GoalStoreProbe.Create();
        var first = JsonStoreSerialization.Encode(PersistedGoalDocument.FromDomain(probeTenant, probeKey, probeRecord), _settings.Encoding.RecordOptions, _settings.MaximumRecordBytes);
        var (tenant, key, record) = JsonStoreSerialization.Decode<PersistedGoalDocument>(first, _settings.Encoding.RecordOptions).ToDomain();
        var second = JsonStoreSerialization.Encode(PersistedGoalDocument.FromDomain(tenant, key, record), _settings.Encoding.RecordOptions, _settings.MaximumRecordBytes);
        if (!first.AsSpan().SequenceEqual(second))
        {
            throw new InvalidOperationException("The configured JSON encoding contract cannot round-trip goal evidence exactly.");
        }
    }

    private void BindManifest(CancellationToken cancellationToken)
    {
        var payload = JsonAtomicDocument.Read(_root.ManifestPath, _settings.MaximumDocumentBytes);
        if (payload is null)
        {
            if (_target.OpenMode != JsonStoreOpenMode.CreateIfMissing)
            {
                throw new InvalidOperationException("The configured JSON goal-store root has no manifest.");
            }

            var created = new JsonStoreManifest(_target.ExpectedStoreInstanceId.Value, _storeKind, _schemaVersion, _settings.Encoding.Fingerprint);
            JsonAtomicDocument.Replace(
                _root.ManifestPath,
                JsonStoreSerialization.Encode(created, _settings.Encoding.DocumentOptions, _settings.MaximumDocumentBytes),
                cancellationToken);
            return;
        }

        var manifest = JsonStoreSerialization.Decode<JsonStoreManifest>(payload, _settings.Encoding.DocumentOptions);
        if (manifest.StoreId != _target.ExpectedStoreInstanceId.Value || !string.Equals(manifest.StoreKind, _storeKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The JSON goal-store identity does not match bootstrap configuration.");
        }

        if (manifest.SchemaVersion != _schemaVersion)
        {
            throw new InvalidOperationException("The JSON goal-store schema version is unsupported.");
        }

        if (!string.Equals(manifest.FormatFingerprint, _settings.Encoding.Fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The JSON goal-store root was written under a different encoding contract.");
        }
    }

    private void Replay(CancellationToken cancellationToken)
    {
        var replay = _log.Replay(cancellationToken);
        if (replay.HasIncompleteTrailingRecord && _target.RecoveryMode != JsonStoreRecoveryMode.RecoverTornAppends)
        {
            throw new InvalidOperationException("The JSON goal-store log ends with an incomplete record.");
        }

        foreach (var record in replay.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (tenant, key, goal) = JsonStoreSerialization.Decode<PersistedGoalDocument>(record.Span, _settings.Encoding.RecordOptions).ToDomain();
            _state.Restore(tenant, key, goal);
        }

        if (replay.HasIncompleteTrailingRecord)
        {
            GoalStoreObservation.Safe(() => JsonGoalStoreLog.RecoveredTornAppend(_logger));
        }

        if (replay.HasIncompleteTrailingRecord || replay.Records.Count > _settings.CompactionRecordThreshold)
        {
            Compact(replay.Records.Count, cancellationToken);
        }
    }

    private void Compact(int replayed, CancellationToken cancellationToken)
    {
        var snapshot = _state.Snapshot();
        _log.Compact(
            [.. snapshot.Select(item => JsonStoreSerialization.Encode(
                PersistedGoalDocument.FromDomain(item.Tenant, item.CreateKey, item.Record),
                _settings.Encoding.RecordOptions,
                _settings.MaximumRecordBytes))],
            cancellationToken);
        GoalStoreObservation.Safe(() => JsonGoalStoreLog.Compacted(_logger, replayed, snapshot.Count));
    }
}
