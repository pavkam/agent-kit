// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Searches vectors of exactly one space with a brute-force scan, persisting every change as a flushed newline-delimited JSON log under one fixed local root.</summary>
/// <remarks>
/// <para>
/// Every acknowledged batch appends one line holding its vectors, removals, and receipt, and is flushed before the call returns.
/// Live state is projected during initialization by replaying the log. Search scores every visible vector under the space's
/// metric, so results are exact and deterministic and the index never claims approximate search. A vector index is derived
/// state: replaying it cannot resurrect deleted data for retrieval because callers filter hits against the authoritative
/// document store.
/// </para>
/// <para>
/// Every request names its complete vector space, and a mismatch is refused before any grant is consumed or state is read. The
/// adapter holds an advisory exclusive lock on its root and rejects a second writer. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class JsonVectorIndex: IVectorIndex, IDisposable
{
    private const string _adapter = "json";
    private const string _storeKind = "agentkit.memory.vectors";
    private const int _snapshotSlice = 256;

    private readonly JsonStoreFile _file;
    private readonly MemoryStoreEnforcement _enforcement;
    private readonly VectorStoreState _state = new();
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly ILogger<JsonVectorIndex> _logger;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes an index bound to one host-authorized root without opening, creating, or locking it.</summary>
    /// <param name="space">The single vector space the index holds.</param>
    /// <param name="target">The non-null exact root and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable bounds and encoding contract.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public JsonVectorIndex(
        VectorSpaceDescriptor space,
        JsonMemoryTarget target,
        JsonMemorySettings settings,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<JsonVectorIndex>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        VectorSpace = space;
        SecurityAudience = new ComponentId("agentkit.vectors.json");
        _enforcement = new MemoryStoreEnforcement(grants, intentIds, SecurityAudience);
        _time = time;
        _logger = logger ?? NullLogger<JsonVectorIndex>.Instance;
        _file = new JsonStoreFile(target, settings, _storeKind, "vectors", "vector", _logger);
    }

    /// <inheritdoc/>
    public VectorSpaceDescriptor VectorSpace { get; }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; }

    /// <inheritdoc/>
    public bool IsDurable => true;

    /// <inheritdoc/>
    public bool ApproximateSearch => false;

    /// <summary>Validates or creates the root, binds its encoding contract, and replays recorded batches into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, identity, encoding contract, or persisted evidence cannot be validated safely, or a second writer holds the advisory lock.</exception>
    /// <exception cref="ObjectDisposedException">The index was disposed.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the index. Repeating it after success is a no-op.</remarks>
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
    public ValueTask<VectorUpsertResult> UpsertAsync(VectorUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Upsert, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (VectorPlanner.CheckSpace(VectorSpace, request.Space) is { } mismatch)
            {
                return VectorUpsertResult.Rejected(mismatch);
            }

            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.CreateOrReplace,
                [VectorSecurityBinding.Resource(VectorSpace.IndexKey)],
                VectorSecurityBinding.UpsertFingerprint(request.Space, request.Records, request.IdempotencyKey), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return VectorUpsertResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                var plan = VectorPlanner.PlanUpsert(_state, request);
                return Persist(request.Grant.Identity.TenantId, plan.Upserts, plan.Deletes, plan.Receipt, plan.Watermark, cancellationToken) is { } failure
                    ? VectorUpsertResult.Rejected(failure)
                    : plan.Result;
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<VectorSearchResult> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Search, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (VectorPlanner.CheckSpace(VectorSpace, request.Space) is { } mismatch)
            {
                return VectorSearchResult.Rejected(mismatch);
            }

            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [VectorSecurityBinding.Resource(VectorSpace.IndexKey)],
                VectorSecurityBinding.SearchFingerprint(request.Space, request.Query, request.TopK, request.Documents), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return VectorSearchResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                return VectorPlanner.Search(_state, VectorSpace, request);
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<VectorDeleteResult> DeleteAsync(VectorDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Delete, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (VectorPlanner.CheckSpace(VectorSpace, request.Space) is { } mismatch)
            {
                return VectorDeleteResult.Rejected(mismatch);
            }

            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                [VectorSecurityBinding.Resource(VectorSpace.IndexKey)],
                VectorSecurityBinding.DeleteFingerprint(request.Space, request.ChunkIds, request.IdempotencyKey), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return VectorDeleteResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                var plan = VectorPlanner.PlanDelete(_state, request);
                return Persist(request.Grant.Identity.TenantId, plan.Upserts, plan.Deletes, plan.Receipt, plan.Watermark, cancellationToken) is { } failure
                    ? VectorDeleteResult.Rejected(failure)
                    : plan.Result;
            }
        }, static result => result.Failure);
    }

    /// <summary>Releases the advisory lock so another writer may open the root.</summary>
    /// <remarks>Disposal is idempotent. Acknowledged batches were already flushed and are unaffected.</remarks>
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
        new(MemoryStoreFailureKind.Unavailable, "The vectors could not be written durably; their commit status is unknown.");

    private MemoryStoreFailure? Persist(
        TenantId tenant,
        ImmutableArray<VectorEntry> upserts,
        ImmutableArray<ChunkId> deletes,
        VectorReceipt? receipt,
        long watermark,
        CancellationToken cancellationToken)
    {
        if (receipt is null)
        {
            return null;
        }

        try
        {
            _file.Append(
                _file.Encode(new VectorLogDocument(
                    tenant.Value,
                    [.. upserts.Select(static entry => VectorRecordDocument.FromDomain(entry.Record))],
                    [.. deletes.Select(static chunk => chunk.Value)],
                    VectorReceiptDocument.FromDomain(receipt))),
                cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return Unwritable();
        }

        _state.Commit(upserts, deletes, receipt, watermark);
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
            var document = _file.Decode<VectorLogDocument>(record.Span);
            var tenant = new TenantId(document.Tenant);
            foreach (var upsert in document.Upserts.IsDefault ? [] : document.Upserts)
            {
                _state.RestoreVector(new VectorEntry(tenant, upsert.ToDomain()));
            }

            foreach (var chunk in document.Deletes.IsDefault ? [] : document.Deletes)
            {
                _state.RestoreDelete(tenant, new ChunkId(chunk));
            }

            if (document.Receipt is { } receipt)
            {
                _state.RestoreReceipt(receipt.ToDomain());
            }
        }

        if (_file.NeedsCompaction)
        {
            Compact(cancellationToken);
        }

        _initialized = true;
    }

    private void Compact(CancellationToken cancellationToken)
    {
        var snapshot = new List<byte[]>();
        var vectors = _state.SnapshotVectors();
        foreach (var slice in vectors.GroupBy(static entry => entry.Tenant).SelectMany(static group => group.Chunk(_snapshotSlice)))
        {
            snapshot.Add(_file.Encode(new VectorLogDocument(slice[0].Tenant.Value, [.. slice.Select(static entry => VectorRecordDocument.FromDomain(entry.Record))], [], null)));
        }

        foreach (var receipt in _state.SnapshotReceipts())
        {
            snapshot.Add(_file.Encode(new VectorLogDocument(receipt.Tenant.Value, [], [], VectorReceiptDocument.FromDomain(receipt))));
        }

        _file.Compact(snapshot, snapshot.Count, cancellationToken);
    }

    private void VerifyEncoding()
    {
        var record = new VectorRecordDocument(
            Guid.Parse("c0000000-0000-0000-0000-000000000001"), Guid.Parse("c0000000-0000-0000-0000-000000000002"), "v1",
            Guid.Parse("c0000000-0000-0000-0000-000000000003"), "probe", "probe", true, [0.1f, -0.25f, 3.0e-7f], "sha256:probe", "c1", DateTimeOffset.UnixEpoch);
        _file.Verify(new VectorLogDocument("probe", [record], [record.ChunkId], new VectorReceiptDocument("probe", "k", "f", 1, 1, false)));
    }

    private ValueTask<TResult> Observe<TResult>(
        MemoryStoreOperationKind kind,
        SecurityGrant grant,
        Func<ValueTask<TResult>> operation,
        Func<TResult, MemoryStoreFailure?> failureOf) =>
        MemoryStoreObservation.ObserveAsync(_logger, _time, _adapter, MemoryStoreFamily.Vector, kind, grant.Identity.TenantId, operation, failureOf);
}
