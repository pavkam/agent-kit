// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores versioned documents, chunk sets, and active-version pointers in tenant-partitioned process memory.</summary>
/// <remarks>
/// <para>
/// State lives only as long as this instance: nothing survives the process, so this adapter is for tests, local development,
/// and compositions that explicitly accept ephemeral documents. It claims no durability.
/// </para>
/// <para>
/// A publication stores a complete version and, when asked, switches the active pointer in the same committed entry, so a
/// reader never observes a mixed set. Every operation consumes a single-use grant that binds that exact operation before state
/// is touched. One gate serializes mutations. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class InMemoryDocumentStore: IDocumentStore
{
    private const string _adapter = "in_memory";

    private readonly Lock _gate = new();
    private readonly DocumentStoreState _state = new();
    private readonly MemoryStoreEnforcement _enforcement;
    private readonly TimeProvider _time;
    private readonly ILogger<InMemoryDocumentStore> _logger;

    /// <summary>Initializes an empty store.</summary>
    /// <param name="key">The key the store is registered and selected under.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact operation grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    public InMemoryDocumentStore(
        DocumentStoreKey key,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<InMemoryDocumentStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        Descriptor = new DocumentStoreDescriptor(key, "agentkit.documents.in-memory", new ComponentId("agentkit.documents.in-memory"), isDurable: false);
        _enforcement = new MemoryStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _time = time;
        _logger = logger ?? NullLogger<InMemoryDocumentStore>.Instance;
    }

    /// <inheritdoc/>
    public DocumentStoreDescriptor Descriptor { get; }

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
                var plan = DocumentPlanner.PlanWrite(_state, request);
                Commit(plan.Upsert);
                return plan.Result;
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
                var plan = DocumentPlanner.PlanActivate(_state, request);
                Commit(plan.Upsert);
                return plan.Result;
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
                var plan = DocumentPlanner.PlanDelete(_state, request, Descriptor.Name);
                Commit(plan.Upsert);
                return plan.Result;
            }
        }, static result => result.Failure);
    }

    private void Commit(DocumentEntry? entry)
    {
        if (entry is not null)
        {
            _state.Commit(entry);
        }
    }

    private ValueTask<TResult> Observe<TResult>(
        MemoryStoreOperationKind kind,
        SecurityGrant grant,
        Func<ValueTask<TResult>> operation,
        Func<TResult, MemoryStoreFailure?> failureOf) =>
        MemoryStoreObservation.ObserveAsync(_logger, _time, _adapter, MemoryStoreFamily.Document, kind, grant.Identity.TenantId, operation, failureOf);
}
