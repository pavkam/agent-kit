// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores durable memory records as a flushed newline-delimited JSON log under one fixed local root.</summary>
/// <remarks>
/// <para>
/// Every acknowledged mutation appends one line holding the complete entries it changed and is flushed before the call returns,
/// so an acknowledged record, transition, or tombstone survives process loss. Live state is projected during initialization by
/// replaying the log through the same shared planner state the in-memory adapter runs; a torn trailing append is recovered or
/// refused according to the target's recovery mode. A tombstone outlives replay, so restoring a log can never resurrect a
/// deleted record for retrieval.
/// </para>
/// <para>
/// The adapter holds an advisory exclusive lock on its root and rejects a second writer, so it claims no multi-process
/// coordination. Every operation consumes a single-use grant that binds that exact operation before any state is read or
/// written. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class JsonMemoryStore: IMemoryStore, IDisposable
{
    private const string _adapter = "json";
    private const string _storeKind = "agentkit.memory.store";

    private readonly JsonStoreFile _file;
    private readonly MemoryStoreEnforcement _enforcement;
    private readonly MemoryStoreState _state = new();
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly ILogger<JsonMemoryStore> _logger;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes a store bound to one host-authorized root without opening, creating, or locking it.</summary>
    /// <param name="key">The key the store is registered and selected under.</param>
    /// <param name="target">The non-null exact root and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable bounds and encoding contract.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    public JsonMemoryStore(
        MemoryStoreKey key,
        JsonMemoryTarget target,
        JsonMemorySettings settings,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<JsonMemoryStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        Descriptor = new MemoryStoreDescriptor(key, "agentkit.memory.json", new ComponentId("agentkit.memory.json"), isDurable: true);
        _enforcement = new MemoryStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _time = time;
        _logger = logger ?? NullLogger<JsonMemoryStore>.Instance;
        _file = new JsonStoreFile(target, settings, _storeKind, "memory", "memory", _logger);
    }

    /// <inheritdoc/>
    public MemoryStoreDescriptor Descriptor { get; }

    /// <summary>Validates or creates the root, binds its encoding contract, and replays recorded entries into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, identity, encoding contract, or persisted evidence cannot be validated safely, or a second writer holds the advisory lock.</exception>
    /// <exception cref="ObjectDisposedException">The store was disposed.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Calling it during trusted host startup surfaces a misconfigured root at boot. Repeating it after success is a no-op.</remarks>
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
    public ValueTask<MemoryWriteResult> WriteAsync(MemoryWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Write, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                [MemorySecurityBinding.Resource(request.Record.Id)], MemorySecurityBinding.WriteFingerprint(request.Record, request.IdempotencyKey), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return MemoryWriteResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                var plan = MemoryPlanner.PlanWrite(_state, request);
                return Persist(plan.Upserts, cancellationToken) is { } failure ? MemoryWriteResult.Rejected(failure) : plan.Result;
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryReadResult> ReadAsync(MemoryReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Read, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [MemorySecurityBinding.Resource(request.Id)], MemorySecurityBinding.ReadFingerprint(request.Id), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return MemoryReadResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                return MemoryPlanner.Read(_state, request);
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryListResult> ListAsync(MemoryListRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.List, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [MemorySecurityBinding.CollectionResource(request.Grant.Scope.AgentId)],
                MemorySecurityBinding.ListFingerprint(request.Namespace, request.States, request.Terms, request.AfterSequence, request.Limit), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return MemoryListResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                return MemoryPlanner.List(_state, request);
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryTransitionResult> TransitionAsync(MemoryTransitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Transition, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                [MemorySecurityBinding.Resource(request.Id)],
                MemorySecurityBinding.TransitionFingerprint(request.Id, request.To, request.ExpectedVersion, request.Replacement, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return MemoryTransitionResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                var plan = MemoryPlanner.PlanTransition(_state, request);
                return Persist(plan.Upserts, cancellationToken) is { } failure ? MemoryTransitionResult.Rejected(failure) : plan.Result;
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<MemoryDeleteResult> DeleteAsync(MemoryDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Delete, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                [MemorySecurityBinding.Resource(request.Id)],
                MemorySecurityBinding.DeleteFingerprint(request.Id, request.ExpectedVersion, request.Mode, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return MemoryDeleteResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                var plan = MemoryPlanner.PlanDelete(_state, request, Descriptor.Name);
                return Persist(plan.Upserts, cancellationToken) is { } failure ? MemoryDeleteResult.Rejected(failure) : plan.Result;
            }
        }, static result => result.Failure);
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
            _file.Dispose();
        }
    }

    private static MemoryStoreFailure Unwritable() =>
        new(MemoryStoreFailureKind.Unavailable, "The memory could not be written durably; its commit status is unknown.");

    private MemoryStoreFailure? Persist(ImmutableArray<MemoryEntry> upserts, CancellationToken cancellationToken)
    {
        if (upserts.IsEmpty)
        {
            return null;
        }

        try
        {
            _file.Append(_file.Encode(new MemoryLogDocument([.. upserts.Select(MemoryEntryDocument.FromDomain)])), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return Unwritable();
        }

        _state.Commit(upserts);
        return null;
    }

    private void EnsureInitialized(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        var records = _file.Open(VerifyEncoding, cancellationToken);
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in _file.Decode<MemoryLogDocument>(record.Span).Entries)
            {
                _state.Restore(entry.ToDomain());
            }
        }

        if (_file.NeedsCompaction)
        {
            var snapshot = _state.Snapshot();
            _file.Compact([.. snapshot.Select(entry => _file.Encode(new MemoryLogDocument([MemoryEntryDocument.FromDomain(entry)])))], snapshot.Count, cancellationToken);
        }

        _initialized = true;
    }

    private void VerifyEncoding()
    {
        var probe = new MemoryRecordDocument(
            Guid.Parse("a0000000-0000-0000-0000-000000000001"), Guid.Parse("a0000000-0000-0000-0000-000000000002"),
            Guid.Parse("a0000000-0000-0000-0000-000000000003"), Guid.Parse("a0000000-0000-0000-0000-000000000004"),
            "probe", "probe", "probe", true, MemoryKind.Summary, "probe text", [new StoreExtensionDocument("k", "AQ==")],
            DataClassification.Internal, new ProvenanceDocument("probe", null, null, null, []), DateTimeOffset.UnixEpoch, true,
            MemoryLifecycleState.Active, "1", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1), []);
        _file.Verify(new MemoryLogDocument([new MemoryEntryDocument("probe", 1, "key", probe, probe, [], DateTimeOffset.UnixEpoch, 1, false)]));
    }

    private ValueTask<TResult> Observe<TResult>(
        MemoryStoreOperationKind kind,
        SecurityGrant grant,
        Func<ValueTask<TResult>> operation,
        Func<TResult, MemoryStoreFailure?> failureOf) =>
        MemoryStoreObservation.ObserveAsync(_logger, _time, _adapter, MemoryStoreFamily.Memory, kind, grant.Identity.TenantId, operation, failureOf);
}
