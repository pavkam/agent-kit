// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Publishes and removes documents across the profile's document store and vector indexes.</summary>
/// <remarks>
/// <para>
/// Publication follows stage, embed, index, activate: the version is written inactive, its chunks are embedded under an egress
/// grant, the vectors are upserted into every index that shares the embedding's vector space, and only then is the active-version
/// pointer switched with the previously observed active version as the expected value. A failure before activation leaves the
/// previous version active. After activation the superseded version's vectors are removed on a best-effort basis; retrieval drops a
/// hit whose version is not active regardless, so a pending cleanup never exposes a stale chunk.
/// </para>
/// <para>
/// Deletion tombstones the document, removes the tombstone receipt's chunk vectors from every profile index, and purges the body
/// only after every index is clean. An index whose cleanup failed is named in the receipt's pending stores. Every store call uses
/// its own single-use grant from the authority the operation context's authorization names. The coordinator is stateless and
/// thread-safe.
/// </para>
/// </remarks>
internal sealed class DefaultDocumentLifecycleCoordinator: IDocumentLifecycleCoordinator
{
    private const int _embeddingBatchSize = 64;

    private static readonly ComponentId _audience = new("agentkit.memory.document-lifecycle");

    private readonly IMemoryProfileRuntimeSelector _runtimes;
    private readonly MemoryGrantIssuer _grants;
    private readonly IDocumentChunker _chunker;
    private readonly TimeProvider _time;
    private readonly ILogger<DefaultDocumentLifecycleCoordinator> _logger;

    /// <summary>Initializes the coordinator.</summary>
    /// <param name="runtimes">The selector that activates the exact profile runtime for each operation.</param>
    /// <param name="grants">The issuer of single-use grants from the captured authority.</param>
    /// <param name="chunker">The chunker that splits source text.</param>
    /// <param name="time">The injected clock for record instants and observation.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultDocumentLifecycleCoordinator(
        IMemoryProfileRuntimeSelector runtimes,
        MemoryGrantIssuer grants,
        IDocumentChunker chunker,
        TimeProvider time,
        ILogger<DefaultDocumentLifecycleCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(runtimes);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(chunker);
        ArgumentNullException.ThrowIfNull(time);
        _runtimes = runtimes;
        _grants = grants;
        _chunker = chunker;
        _time = time;
        _logger = logger ?? NullLogger<DefaultDocumentLifecycleCoordinator>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<DocumentPublishResult> PublishAsync(DocumentPublishCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await RunAsync(
            "publish", AgentKitActivityNames.MemoryDocumentPublish, command.Id, command.Context,
            (lease, token) => PublishCoreAsync(command, lease, token),
            static result => result.IsPublished ? null : FailureName(result.Failure.Kind),
            static failure => DocumentPublishResult.Rejected(failure),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<DocumentRemovalResult> DeleteAsync(DocumentRemovalCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await RunAsync(
            "delete", AgentKitActivityNames.MemoryDocumentDelete, command.Id, command.Context,
            (lease, token) => DeleteCoreAsync(command, lease, token),
            static result => result.IsDeleted ? null : FailureName(result.Failure.Kind),
            static failure => DocumentRemovalResult.Rejected(failure),
            cancellationToken).ConfigureAwait(false);
    }

    private static string FailureName(MemoryStoreFailureKind kind) => kind switch
    {
        MemoryStoreFailureKind.Denied => "denied",
        MemoryStoreFailureKind.NotFound => "not_found",
        MemoryStoreFailureKind.VersionConflict => "version_conflict",
        MemoryStoreFailureKind.IdempotencyConflict => "idempotency_conflict",
        MemoryStoreFailureKind.InvalidTransition => "invalid_transition",
        MemoryStoreFailureKind.ScopeMismatch => "scope_mismatch",
        MemoryStoreFailureKind.IncompatibleVectorSpace => "incompatible_vector_space",
        MemoryStoreFailureKind.LimitExceeded => "limit_exceeded",
        MemoryStoreFailureKind.Unavailable => "unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The memory-store failure class is undefined."),
    };

    private async ValueTask<TResult> RunAsync<TResult>(
        string operation,
        string activityName,
        DocumentId documentId,
        MemoryOperationContext context,
        Func<IMemoryProfileRuntimeLease, CancellationToken, ValueTask<TResult>> work,
        Func<TResult, string?> refusalOf,
        Func<MemoryStoreFailure, TResult> unavailable,
        CancellationToken cancellationToken)
    {
        var started = MemoryObservation.TryTimestamp(_time);
        var metricName = $"document_{operation}";
        using var scope = AgentKitActivityScope.Start(
            activityName,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.MemoryDocumentId, documentId.ToString()),
                new(AgentKitTagNames.AgentId, context.AgentId.ToString()),
                new(AgentKitTagNames.TenantId, context.Identity.TenantId.Value),
                new(AgentKitTagNames.MemoryProfileKey, context.ProfileKey.Value),
            ]);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selection = await _runtimes.SelectAsync(context, cancellationToken).ConfigureAwait(false);
            if (selection is not MemoryProfileRuntimeSelected selected)
            {
                var failure = new MemoryStoreFailure(MemoryStoreFailureKind.Unavailable, ((MemoryProfileRuntimeUnavailable) selection).Failure.SafeMessage);
                return Finish(scope, operation, metricName, documentId, started, unavailable(failure), "unavailable");
            }

            await using var lease = selected.Runtime;
            var result = await work(lease, cancellationToken).ConfigureAwait(false);
            return Finish(scope, operation, metricName, documentId, started, result, refusalOf(result));
        }
        catch (OperationCanceledException)
        {
            MemoryObservation.Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            MemoryObservation.Safe(() => MemoryLog.DocumentOperationCompleted(_logger, LogLevel.Information, operation, "cancelled", documentId));
            MemoryObservation.Safe(() => MemoryObservation.RecordOperation(metricName, "cancelled", MemoryObservation.TryElapsed(_time, started)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            MemoryObservation.Safe(() => scope.Activity.SetFailed("faulted", errorType));
            MemoryObservation.Safe(() => MemoryLog.DocumentOperationCompleted(_logger, LogLevel.Error, operation, "faulted", documentId));
            MemoryObservation.Safe(() => MemoryObservation.RecordOperation(metricName, "faulted", MemoryObservation.TryElapsed(_time, started)));
            throw;
        }
    }

    private TResult Finish<TResult>(AgentKitActivityScope scope, string operation, string metricName, DocumentId documentId, long? started, TResult result, string? refusal)
    {
        var outcome = refusal ?? "completed";
        MemoryObservation.Safe(() =>
        {
            if (refusal is null)
            {
                scope.Activity.SetSuccessful(outcome);
            }
            else
            {
                scope.Activity.SetFailed(outcome, outcome);
            }
        });
        MemoryObservation.Safe(() => MemoryLog.DocumentOperationCompleted(_logger, refusal is null ? LogLevel.Information : LogLevel.Warning, operation, outcome, documentId));
        MemoryObservation.Safe(() => MemoryObservation.RecordOperation(metricName, outcome, MemoryObservation.TryElapsed(_time, started)));
        return result;
    }

    private async ValueTask<DocumentPublishResult> PublishCoreAsync(DocumentPublishCommand command, IMemoryProfileRuntimeLease lease, CancellationToken cancellationToken)
    {
        var profile = lease.Profile;
        if (lease.DocumentStore is not { } store)
        {
            return Reject(MemoryStoreFailureKind.Unavailable, "The memory profile names no document store.");
        }

        if (command.Classification > profile.MaximumClassification)
        {
            return Reject(MemoryStoreFailureKind.Denied, "The document's classification exceeds the profile's ceiling.");
        }

        var runId = (command.Context.Correlation as InRunOperationCorrelation)?.RunId ?? command.Provenance.SourceRunId;
        if (runId is not { } sourceRun)
        {
            return Reject(MemoryStoreFailureKind.ScopeMismatch, "The publication names no source run.");
        }

        var chunks = _chunker.Chunk(command.Id, command.Version, command.Text);
        if (chunks.IsDefaultOrEmpty || chunks.Length > DocumentWriteRequest.MaximumChunks)
        {
            return Reject(MemoryStoreFailureKind.LimitExceeded, "The document produced no chunks or more chunks than one version may hold.");
        }

        var active = await ReadActiveAsync(command, lease, store, cancellationToken).ConfigureAwait(false);
        if (active.Failure is not null)
        {
            return DocumentPublishResult.Rejected(active.Failure);
        }

        var contentHash = new ContentHash($"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(command.Text)))}");
        if (active.Record is { } current && current.Version == command.Version)
        {
            return current.ContentHash == contentHash
                ? DocumentPublishResult.Published(current, chunks.Length, 0, null, replayed: true)
                : Reject(MemoryStoreFailureKind.IdempotencyConflict, "The document version is already active with different content.");
        }

        ImmutableArray<EmbeddedChunk> embedded = [];
        if (!lease.VectorIndexes.IsEmpty)
        {
            var embedding = await EmbedAsync(command, lease, chunks, cancellationToken).ConfigureAwait(false);
            if (embedding.Failure is not null)
            {
                return DocumentPublishResult.Rejected(embedding.Failure);
            }

            embedded = embedding.Chunks;
        }

        var identity = command.Context.Identity;
        var record = new DocumentRecord(
            command.Id, command.Context.AgentId, command.Context.SessionId, sourceRun, identity.TenantId,
            new PrincipalVisibility(identity.TenantId, identity.PrincipalId, command.ShareWithTenant),
            command.Version, contentHash,
            command.Metadata, command.Classification, command.Provenance, command.Retention);
        var now = _time.GetUtcNow();
        var stageKey = new IdempotencyKey($"agentkit.document.stage:{command.IdempotencyKey.Value}");
        var stageIssue = await _grants.IssueAsync(
            lease.SecurityAuthorities, command.Context.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.CreateOrReplace,
            [DocumentSecurityBinding.Resource(record.Id)], DocumentSecurityBinding.WriteFingerprint(record, chunks, false, stageKey, now), cancellationToken).ConfigureAwait(false);
        if (stageIssue.Grant is null)
        {
            return DocumentPublishResult.Rejected(stageIssue.ToFailure());
        }

        var staged = await store.WriteAsync(new DocumentWriteRequest(record, chunks, false, stageKey, now, stageIssue.Grant), cancellationToken).ConfigureAwait(false);
        if (!staged.IsWritten)
        {
            return DocumentPublishResult.Rejected(staged.Failure);
        }

        var indexed = 0;
        if (!embedded.IsEmpty)
        {
            var indexing = await IndexAsync(command, lease, record, embedded, cancellationToken).ConfigureAwait(false);
            if (indexing.Failure is not null)
            {
                return DocumentPublishResult.Rejected(indexing.Failure);
            }

            indexed = indexing.Indexed;
        }

        var activateKey = new IdempotencyKey($"agentkit.document.activate:{command.IdempotencyKey.Value}");
        var activateIssue = await _grants.IssueAsync(
            lease.SecurityAuthorities, command.Context.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
            [DocumentSecurityBinding.Resource(record.Id)],
            DocumentSecurityBinding.ActivateFingerprint(record.Id, record.Version, active.Record?.Version, activateKey, now), cancellationToken).ConfigureAwait(false);
        if (activateIssue.Grant is null)
        {
            return DocumentPublishResult.Rejected(activateIssue.ToFailure());
        }

        var activated = await store.ActivateAsync(
            new DocumentActivateRequest(record.Id, record.Version, active.Record?.Version, activateKey, now, activateIssue.Grant), cancellationToken).ConfigureAwait(false);
        if (!activated.IsActivated)
        {
            return DocumentPublishResult.Rejected(activated.Failure);
        }

        if (activated.PreviousActiveVersion is { } previous && previous != record.Version)
        {
            await RemoveSupersededAsync(command, lease, store, previous, cancellationToken).ConfigureAwait(false);
        }

        await PublishEventAsync(lease, command.Context, MemoryEventKind.DocumentPublished, command.Id, "published", chunks.Length, cancellationToken).ConfigureAwait(false);
        return DocumentPublishResult.Published(activated.Record, chunks.Length, indexed, activated.PreviousActiveVersion, replayed: false);
    }

    private async ValueTask<ActiveRead> ReadActiveAsync(
        DocumentPublishCommand command,
        IMemoryProfileRuntimeLease lease,
        IDocumentStore store,
        CancellationToken cancellationToken)
    {
        var issue = await _grants.IssueAsync(
            lease.SecurityAuthorities, command.Context.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            [DocumentSecurityBinding.Resource(command.Id)], DocumentSecurityBinding.ReadFingerprint(command.Id, null, false), cancellationToken).ConfigureAwait(false);
        if (issue.Grant is null)
        {
            return new ActiveRead(null, issue.ToFailure());
        }

        var read = await store.ReadAsync(new DocumentReadRequest(command.Id, null, false, issue.Grant), cancellationToken).ConfigureAwait(false);
        return read.IsFound && read.State == DocumentVersionState.Active
            ? new ActiveRead(read.Record, null)
            : read.IsFound || read.Failure?.Kind == MemoryStoreFailureKind.NotFound
                ? new ActiveRead(null, null)
                : new ActiveRead(null, read.Failure ?? new MemoryStoreFailure(MemoryStoreFailureKind.Unavailable, "The document could not be read."));
    }

    private static DocumentPublishResult Reject(MemoryStoreFailureKind kind, string message) => DocumentPublishResult.Rejected(new MemoryStoreFailure(kind, message));

    private async ValueTask<EmbeddingOutcome> EmbedAsync(
        DocumentPublishCommand command,
        IMemoryProfileRuntimeLease lease,
        ImmutableArray<DocumentChunk> chunks,
        CancellationToken cancellationToken)
    {
        if (lease.Profile.Embedding is not { } reference || lease.EmbeddingSelector is not { } selector || lease.EmbeddingExecutor is not { } executor)
        {
            return EmbeddingOutcome.Fail(MemoryStoreFailureKind.Unavailable, "The profile names vector indexes but no embedding model selection.");
        }

        var issue = await _grants.IssueAsync(
            lease.SecurityAuthorities, command.Context.Authorization, _audience, SecurityOperationKind.StateRead, SecurityEffect.Egress,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, $"document-embedding:{command.Id}:{command.Version.Value}")],
            Fingerprint("document-embedding", command.Id.ToString(), command.Version.Value, lease.Profile.ConfigurationFingerprint.Value), cancellationToken).ConfigureAwait(false);
        if (issue.Grant is null)
        {
            return EmbeddingOutcome.Fail(issue.IsUnavailable ? MemoryStoreFailureKind.Unavailable : MemoryStoreFailureKind.Denied, issue.SafeMessage ?? "The embedding egress was not authorized.");
        }

        var operation = new ProtectedSemanticOperationContext(
            command.Context.AgentId, command.Context.SessionId, null, command.Context.Identity, command.Context.Correlation, command.Context.Authorization);
        var selection = await selector.SelectAsync(
            new EmbeddingSelectionRequest(operation, reference.Policy, EmbeddingRequirements.None, lease.Models), cancellationToken).ConfigureAwait(false);
        if (selection is not EmbeddingModelSelected selected)
        {
            return EmbeddingOutcome.Fail(MemoryStoreFailureKind.Unavailable, "No compatible embedding model is available.");
        }

        var results = ImmutableArray.CreateBuilder<EmbeddedChunk>(chunks.Length);
        for (var offset = 0; offset < chunks.Length; offset += _embeddingBatchSize)
        {
            var batch = chunks.Skip(offset).Take(_embeddingBatchSize).ToImmutableArray();
            EmbeddingExecutionResult execution;
            try
            {
                execution = await executor.ExecuteAsync(
                    new EmbeddingExecutionRequest(
                        operation,
                        selected.Decision,
                        new EmbeddingRequest(
                            [.. batch.Select(static chunk => (EmbeddingInput) new TextEmbeddingInput(chunk.Text, null))],
                            EmbeddingPurpose.Document, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty),
                        lease.Budget,
                        null,
                        SemanticOperationRetryPolicy.None),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return EmbeddingOutcome.Fail(MemoryStoreFailureKind.Unavailable, "The embedding request failed.");
            }

            if (execution is not EmbeddingExecutionCompleted completed || completed.Response.Items.Length != batch.Length)
            {
                return EmbeddingOutcome.Fail(MemoryStoreFailureKind.Unavailable, "The embedding request did not complete for every chunk.");
            }

            foreach (var item in completed.Response.Items.OrderBy(static item => item.InputIndex))
            {
                if (item is not EmbeddingItemSucceeded { Vector: DenseFloatVector vector } succeeded
                    || item.InputIndex < 0 || item.InputIndex >= batch.Length
                    || vector.Values.Length != succeeded.Space.Dimensions)
                {
                    return EmbeddingOutcome.Fail(MemoryStoreFailureKind.Unavailable, "The embedding of a chunk failed or was malformed.");
                }

                results.Add(new EmbeddedChunk(batch[item.InputIndex], vector.Values, succeeded.Space));
            }
        }

        return results.Count == chunks.Length && results.Select(static item => item.Chunk.Id).Distinct().Count() == chunks.Length
            && results.All(item => item.Space.IsSameVectorSpaceAs(results[0].Space))
            ? EmbeddingOutcome.Ok([.. results])
            : EmbeddingOutcome.Fail(MemoryStoreFailureKind.Unavailable, "The embedding results did not cover every chunk in one vector space.");
    }

    private async ValueTask<IndexOutcome> IndexAsync(
        DocumentPublishCommand command,
        IMemoryProfileRuntimeLease lease,
        DocumentRecord record,
        ImmutableArray<EmbeddedChunk> embedded,
        CancellationToken cancellationToken)
    {
        var space = embedded[0].Space;
        var compatible = lease.VectorIndexes.Where(index => index.VectorSpace.EmbeddingSpace.IsSameVectorSpaceAs(space)).ToImmutableArray();
        if (compatible.IsEmpty)
        {
            return IndexOutcome.Fail(MemoryStoreFailureKind.IncompatibleVectorSpace, "No profile vector index shares the embedding model's vector space.");
        }

        var now = _time.GetUtcNow();
        var records = embedded
            .OrderBy(static item => item.Chunk.Ordinal)
            .Select(item => new VectorRecord(
                item.Chunk.Id, record.Id, record.Version, record.AgentId, record.Visibility, item.Vector, item.Chunk.Hash, item.Chunk.Chunker, now))
            .ToImmutableArray();
        var indexed = 0;
        foreach (var index in compatible)
        {
            var target = new VectorSpaceDescriptor(index.VectorSpace.IndexKey, space, index.VectorSpace.DistanceMetric);
            foreach (var batch in records.Chunk(VectorUpsertRequest.MaximumBatch))
            {
                var key = new IdempotencyKey($"agentkit.document.index:{command.IdempotencyKey.Value}:{index.VectorSpace.IndexKey.Value}:{batch[0].ChunkId}");
                ImmutableArray<VectorRecord> part = [.. batch];
                var issue = await _grants.IssueAsync(
                    lease.SecurityAuthorities, command.Context.Authorization, index.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.CreateOrReplace,
                    [VectorSecurityBinding.Resource(index.VectorSpace.IndexKey)], VectorSecurityBinding.UpsertFingerprint(target, part, key), cancellationToken).ConfigureAwait(false);
                if (issue.Grant is null)
                {
                    return IndexOutcome.Fail(issue.ToFailure().Kind, issue.SafeMessage ?? "The vector upsert was not authorized.");
                }

                var upserted = await index.UpsertAsync(new VectorUpsertRequest(target, part, key, issue.Grant), cancellationToken).ConfigureAwait(false);
                if (!upserted.IsUpserted)
                {
                    return IndexOutcome.Fail(upserted.Failure.Kind, upserted.Failure.SafeMessage);
                }

                indexed += upserted.Upserted;
            }
        }

        return IndexOutcome.Ok(indexed);
    }

    private async ValueTask RemoveSupersededAsync(
        DocumentPublishCommand command,
        IMemoryProfileRuntimeLease lease,
        IDocumentStore store,
        DocumentVersion previous,
        CancellationToken cancellationToken)
    {
        if (lease.VectorIndexes.IsEmpty)
        {
            return;
        }

        var readIssue = await _grants.IssueAsync(
            lease.SecurityAuthorities, command.Context.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            [DocumentSecurityBinding.Resource(command.Id)], DocumentSecurityBinding.ReadFingerprint(command.Id, previous, true), cancellationToken).ConfigureAwait(false);
        if (readIssue.Grant is null)
        {
            MemoryObservation.Safe(() => MemoryLog.DocumentCleanupPending(_logger, command.Id, lease.VectorIndexes.Length));
            return;
        }

        var old = await store.ReadAsync(new DocumentReadRequest(command.Id, previous, true, readIssue.Grant), cancellationToken).ConfigureAwait(false);
        if (!old.IsFound)
        {
            MemoryObservation.Safe(() => MemoryLog.DocumentCleanupPending(_logger, command.Id, lease.VectorIndexes.Length));
            return;
        }

        var pending = await RemoveVectorsAsync(command.Context, lease, [.. old.Chunks.Select(static chunk => chunk.Id)], $"supersede:{command.IdempotencyKey.Value}", cancellationToken).ConfigureAwait(false);
        if (!pending.Pending.IsEmpty)
        {
            MemoryObservation.Safe(() => MemoryLog.DocumentCleanupPending(_logger, command.Id, pending.Pending.Length));
        }
    }

    private async ValueTask<VectorCleanup> RemoveVectorsAsync(
        MemoryOperationContext context,
        IMemoryProfileRuntimeLease lease,
        ImmutableArray<ChunkId> chunkIds,
        string keyBasis,
        CancellationToken cancellationToken)
    {
        var pending = ImmutableArray.CreateBuilder<string>();
        var removed = 0;
        if (chunkIds.IsEmpty)
        {
            return new VectorCleanup(0, []);
        }

        foreach (var index in lease.VectorIndexes)
        {
            var key = new IdempotencyKey($"agentkit.document.unindex:{keyBasis}:{index.VectorSpace.IndexKey.Value}");
            var issue = await _grants.IssueAsync(
                lease.SecurityAuthorities, context.Authorization, index.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                [VectorSecurityBinding.Resource(index.VectorSpace.IndexKey)], VectorSecurityBinding.DeleteFingerprint(index.VectorSpace, chunkIds, key), cancellationToken).ConfigureAwait(false);
            if (issue.Grant is null)
            {
                pending.Add(index.VectorSpace.IndexKey.Value);
                continue;
            }

            var deleted = await index.DeleteAsync(new VectorDeleteRequest(index.VectorSpace, chunkIds, key, issue.Grant), cancellationToken).ConfigureAwait(false);
            if (deleted.IsDeleted)
            {
                removed += deleted.Deleted;
            }
            else
            {
                pending.Add(index.VectorSpace.IndexKey.Value);
            }
        }

        return new VectorCleanup(removed, pending.ToImmutable());
    }

    private async ValueTask<DocumentRemovalResult> DeleteCoreAsync(DocumentRemovalCommand command, IMemoryProfileRuntimeLease lease, CancellationToken cancellationToken)
    {
        if (lease.DocumentStore is not { } store)
        {
            return DocumentRemovalResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.Unavailable, "The memory profile names no document store."));
        }

        var tombstone = await DeleteStepAsync(command, lease, store, DocumentDeleteMode.Tombstone, "tombstone", cancellationToken).ConfigureAwait(false);
        if (!tombstone.IsDeleted)
        {
            return DocumentRemovalResult.Rejected(tombstone.Failure);
        }

        var receipt = tombstone.Receipt;
        var cleanup = await RemoveVectorsAsync(command.Context, lease, receipt.ChunkIds, $"delete:{command.IdempotencyKey.Value}", cancellationToken).ConfigureAwait(false);
        if (command.Mode == DocumentDeleteMode.Purge && cleanup.Pending.IsEmpty && !receipt.PhysicallyPurged)
        {
            var purge = await DeleteStepAsync(command, lease, store, DocumentDeleteMode.Purge, "purge", cancellationToken).ConfigureAwait(false);
            if (purge.IsDeleted)
            {
                receipt = purge.Receipt;
            }
            else
            {
                cleanup = cleanup with { Pending = cleanup.Pending.Add(store.Descriptor.Key.Value) };
            }
        }
        else if (command.Mode == DocumentDeleteMode.Purge && !receipt.PhysicallyPurged)
        {
            cleanup = cleanup with { Pending = cleanup.Pending.Add(store.Descriptor.Key.Value) };
        }

        var merged = receipt.PendingStores.AddRange(cleanup.Pending.Where(pending => !receipt.PendingStores.Contains(pending)));
        var final = new DocumentDeletionReceipt(
            receipt.Id, receipt.LogicallyDeleted, receipt.PhysicallyPurged, receipt.ChunkIds, merged, receipt.DeletedAt, receipt.Generation);
        if (!final.PendingStores.IsEmpty)
        {
            MemoryObservation.Safe(() => MemoryLog.DocumentCleanupPending(_logger, command.Id, final.PendingStores.Length));
        }

        await PublishEventAsync(lease, command.Context, MemoryEventKind.DocumentDeleted, command.Id, final.PhysicallyPurged ? "purged" : "tombstoned", cleanup.Removed, cancellationToken).ConfigureAwait(false);
        return DocumentRemovalResult.Deleted(final, cleanup.Removed, tombstone.Replayed);
    }

    private async ValueTask<DocumentDeleteResult> DeleteStepAsync(
        DocumentRemovalCommand command,
        IMemoryProfileRuntimeLease lease,
        IDocumentStore store,
        DocumentDeleteMode mode,
        string step,
        CancellationToken cancellationToken)
    {
        var key = new IdempotencyKey($"agentkit.document.{step}:{command.IdempotencyKey.Value}");
        var now = _time.GetUtcNow();
        var issue = await _grants.IssueAsync(
            lease.SecurityAuthorities, command.Context.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
            [DocumentSecurityBinding.Resource(command.Id)], DocumentSecurityBinding.DeleteFingerprint(command.Id, mode, key, now), cancellationToken).ConfigureAwait(false);
        return issue.Grant is null
            ? DocumentDeleteResult.Rejected(issue.ToFailure())
            : await store.DeleteAsync(new DocumentDeleteRequest(command.Id, mode, key, now, issue.Grant), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask PublishEventAsync(
        IMemoryProfileRuntimeLease lease,
        MemoryOperationContext context,
        MemoryEventKind kind,
        DocumentId documentId,
        string outcome,
        int count,
        CancellationToken cancellationToken)
    {
        var dispatch = await lease.Events.PublishAsync(
            lease.Profile.Key,
            new MemoryEvent(
                kind, context.Identity.TenantId, context.AgentId, context.SessionId, lease.Profile.Key, lease.Profile.Version, null, documentId, null, outcome, count, _time.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        if (!dispatch.RequiredDeliveryComplete)
        {
            MemoryObservation.Safe(() => MemoryLog.DocumentOperationCompleted(_logger, LogLevel.Error, "observe", "required_sink_missing", documentId));
        }
    }

    private static InputFingerprint Fingerprint(params string[] parts) =>
        new($"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', parts))))}");

    private sealed record EmbeddedChunk(DocumentChunk Chunk, ImmutableArray<float> Vector, EmbeddingSpaceIdentity Space);

    private sealed record EmbeddingOutcome(ImmutableArray<EmbeddedChunk> Chunks, MemoryStoreFailure? Failure)
    {
        internal static EmbeddingOutcome Ok(ImmutableArray<EmbeddedChunk> chunks) => new(chunks, null);

        internal static EmbeddingOutcome Fail(MemoryStoreFailureKind kind, string message) => new([], new MemoryStoreFailure(kind, message));
    }

    private sealed record IndexOutcome(int Indexed, MemoryStoreFailure? Failure)
    {
        internal static IndexOutcome Ok(int indexed) => new(indexed, null);

        internal static IndexOutcome Fail(MemoryStoreFailureKind kind, string message) => new(0, new MemoryStoreFailure(kind, message));
    }

    private sealed record ActiveRead(DocumentRecord? Record, MemoryStoreFailure? Failure);

    private sealed record VectorCleanup(int Removed, ImmutableArray<string> Pending);
}
