// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json;

/// <summary>Holds caller-owned reference-commit intents durably in a flushed newline-delimited JSON log under one fixed local root.</summary>
/// <remarks>
/// <para>
/// Every recorded intent and transition appends one flushed record before it is acknowledged, so a crash between artifact finalization
/// and the caller's reference commit leaves an explicit pending intent for reconciliation. Live state is projected by replaying the log
/// when the store is first used; a torn trailing append is recovered or refused according to the target's recovery mode. A late
/// reference commit and a fence race through one conditional transition under a lock, so exactly one wins; every operation is
/// tenant-qualified, so another tenant's intent is indistinguishable from an absent one. Storage failures propagate as exceptions and
/// leave the projection unchanged.
/// </para>
/// <para>
/// The root must not be shared with an artifact store or another intent store: its manifest binds a distinct store kind and the store
/// holds an advisory exclusive lock, so a second writer is rejected and no multi-process coordination or atomicity with the caller's
/// records is claimed. The instance is thread-safe; dispose it to release the root.
/// </para>
/// </remarks>
public sealed class JsonArtifactReferenceCommitIntentStore: IArtifactReferenceCommitIntentStore, IDisposable
{
    private readonly JsonArtifactReferenceCommitIntentBackend _backend;

    /// <summary>Initializes a store bound to one host-authorized root without opening, creating, or locking it.</summary>
    /// <param name="target">The non-null exact root and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable bounds and encoding contract.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public JsonArtifactReferenceCommitIntentStore(
        JsonArtifactTarget target,
        JsonArtifactSettings settings,
        TimeProvider time,
        ILogger<JsonArtifactReferenceCommitIntentStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(time);
        var resolvedLogger = (ILogger?) logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _backend = new JsonArtifactReferenceCommitIntentBackend(
            new JsonArtifactFile(target, settings, JsonArtifactFileKind.Intents, resolvedLogger), time, resolvedLogger);
    }

    /// <summary>Validates or creates the root, binds its encoding contract, and replays recorded intents into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, identity, store kind, encoding contract, or persisted evidence cannot be validated safely, or a second writer holds the advisory lock.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _backend.Initialize(cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactReferenceCommitIntentResult> RecordAsync(ArtifactReferenceCommitIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_backend.Record(intent, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactReferenceCommitIntentResult> GetAsync(TenantId tenantId, ArtifactPreparationId preparationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_backend.Get(tenantId, preparationId, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactReferenceCommitIntentResult> TransitionAsync(
        TenantId tenantId,
        ArtifactPreparationId preparationId,
        ArtifactReferenceCommitState expected,
        ArtifactReferenceCommitState nextState,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(expected);
        ArgumentOutOfRangeException.ThrowIfUndefined(nextState);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_backend.Transition(tenantId, preparationId, expected, nextState, at, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<ArtifactReferenceCommitIntent>> ListPendingAsync(
        TenantId tenantId,
        DateTimeOffset recordedBefore,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_backend.ListPending(tenantId, recordedBefore, limit, cancellationToken));
    }

    /// <summary>Releases the advisory lock so another writer may open the root.</summary>
    /// <remarks>Disposal is idempotent. Acknowledged records were already flushed and are unaffected.</remarks>
    public void Dispose() => _backend.Dispose();
}
