// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores versioned documents, chunk sets, and active-version pointers as a flushed newline-delimited JSON log under one fixed local root.</summary>
/// <remarks>
/// <para>
/// Every acknowledged mutation appends one line holding the complete document entry it changed, including the active-version
/// pointer, and is flushed before the call returns. Because the pointer and the versions it selects live in one line, a
/// publication is atomic: replay sees either the prior complete state or the new complete state, never a mixed set. A torn
/// trailing append is recovered or refused according to the target's recovery mode, leaving the prior version active.
/// </para>
/// <para>
/// A line carries the whole document, so this adapter suits inspectable local stores of modest documents. The adapter holds an
/// advisory exclusive lock on its root and rejects a second writer. Every operation consumes a single-use grant that binds that
/// exact operation. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class JsonDocumentStore: IDocumentStore, IDisposable
{
    private const string _adapter = "json";
    private const string _storeKind = "agentkit.memory.documents";

    private readonly JsonStoreFile _file;
    private readonly MemoryStoreEnforcement _enforcement;
    private readonly DocumentStoreState _state = new();
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly ILogger<JsonDocumentStore> _logger;
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
    public JsonDocumentStore(
        DocumentStoreKey key,
        JsonMemoryTarget target,
        JsonMemorySettings settings,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<JsonDocumentStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        Descriptor = new DocumentStoreDescriptor(key, "agentkit.documents.json", new ComponentId("agentkit.documents.json"), isDurable: true);
        _enforcement = new MemoryStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _time = time;
        _logger = logger ?? NullLogger<JsonDocumentStore>.Instance;
        _file = new JsonStoreFile(target, settings, _storeKind, "documents", "document", _logger);
    }

    /// <inheritdoc/>
    public DocumentStoreDescriptor Descriptor { get; }

    /// <summary>Validates or creates the root, binds its encoding contract, and replays recorded entries into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, identity, encoding contract, or persisted evidence cannot be validated safely, or a second writer holds the advisory lock.</exception>
    /// <exception cref="ObjectDisposedException">The store was disposed.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Repeating it after success is a no-op.</remarks>
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
    public ValueTask<DocumentWriteResult> WriteAsync(DocumentWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Write, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.CreateOrReplace,
                [DocumentSecurityBinding.Resource(request.Record.Id)],
                DocumentSecurityBinding.WriteFingerprint(request.Record, request.Chunks, request.Activate, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return DocumentWriteResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                var plan = DocumentPlanner.PlanWrite(_state, request);
                return Persist(plan.Upsert, cancellationToken) is { } failure ? DocumentWriteResult.Rejected(failure) : plan.Result;
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentActivateResult> ActivateAsync(DocumentActivateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Activate, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                [DocumentSecurityBinding.Resource(request.Id)],
                DocumentSecurityBinding.ActivateFingerprint(request.Id, request.Version, request.ExpectedActiveVersion, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return DocumentActivateResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                var plan = DocumentPlanner.PlanActivate(_state, request);
                return Persist(plan.Upsert, cancellationToken) is { } failure ? DocumentActivateResult.Rejected(failure) : plan.Result;
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentReadResult> ReadAsync(DocumentReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Read, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [DocumentSecurityBinding.Resource(request.Id)],
                DocumentSecurityBinding.ReadFingerprint(request.Id, request.Version, request.IncludeChunks), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return DocumentReadResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                return DocumentPlanner.Read(_state, request, Descriptor.Name);
            }
        }, static result => result.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentDeleteResult> DeleteAsync(DocumentDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(MemoryStoreOperationKind.Delete, request.Grant, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _enforcement.ConsumeAsync(
                request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                [DocumentSecurityBinding.Resource(request.Id)],
                DocumentSecurityBinding.DeleteFingerprint(request.Id, request.Mode, request.IdempotencyKey, request.At), cancellationToken).ConfigureAwait(false) is { } denial)
            {
                return DocumentDeleteResult.Rejected(denial);
            }

            lock (_gate)
            {
                EnsureInitialized(cancellationToken);
                var plan = DocumentPlanner.PlanDelete(_state, request, Descriptor.Name);
                return Persist(plan.Upsert, cancellationToken) is { } failure ? DocumentDeleteResult.Rejected(failure) : plan.Result;
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
        new(MemoryStoreFailureKind.Unavailable, "The document could not be written durably; its commit status is unknown.");

    private MemoryStoreFailure? Persist(DocumentEntry? entry, CancellationToken cancellationToken)
    {
        if (entry is null)
        {
            return null;
        }

        try
        {
            _file.Append(_file.Encode(DocumentEntryDocument.FromDomain(entry)), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return Unwritable();
        }

        _state.Commit(entry);
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
            _state.Restore(_file.Decode<DocumentEntryDocument>(record.Span).ToDomain());
        }

        if (_file.NeedsCompaction)
        {
            var snapshot = _state.Snapshot();
            _file.Compact([.. snapshot.Select(entry => _file.Encode(DocumentEntryDocument.FromDomain(entry)))], snapshot.Count, cancellationToken);
        }

        _initialized = true;
    }

    private void VerifyEncoding()
    {
        var record = new DocumentRecordDocument(
            Guid.Parse("b0000000-0000-0000-0000-000000000001"), Guid.Parse("b0000000-0000-0000-0000-000000000002"),
            Guid.Parse("b0000000-0000-0000-0000-000000000003"), Guid.Parse("b0000000-0000-0000-0000-000000000004"),
            "probe", "probe", false, "v1", "sha256:probe", "probe", "text/plain", null, [new StoreExtensionDocument("k", "AQ==")],
            DataClassification.Internal, new ProvenanceDocument("probe", null, null, null, []), null, true);
        var chunk = new DocumentChunkDocument(Guid.Parse("b0000000-0000-0000-0000-000000000005"), record.Id, "v1", "c1", 0, "probe chunk", "sha256:c");
        _file.Verify(new DocumentEntryDocument(
            "probe", 1, record.Id, record.AgentId, "probe", false, [new DocumentVersionDocument(record, [chunk], DocumentVersionState.Active)], "v1",
            [new DocumentReceiptDocument("k", "f", "v1")], [chunk.Id], DateTimeOffset.UnixEpoch, 1, false));
    }

    private ValueTask<TResult> Observe<TResult>(
        MemoryStoreOperationKind kind,
        SecurityGrant grant,
        Func<ValueTask<TResult>> operation,
        Func<TResult, MemoryStoreFailure?> failureOf) =>
        MemoryStoreObservation.ObserveAsync(_logger, _time, _adapter, MemoryStoreFamily.Document, kind, grant.Identity.TenantId, operation, failureOf);
}
