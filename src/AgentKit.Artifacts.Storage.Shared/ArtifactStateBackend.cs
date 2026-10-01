// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Executes planned lifecycle operations over an in-memory projection that an adapter persists and replays.</summary>
/// <remarks>
/// <para>
/// Every operation runs under one asynchronous gate, so planning and commit are atomic within the process. A plan is committed in a
/// fixed order: stage the payload, persist the entries, apply them to the projection, then release unreferenced payloads.
/// A persistence failure before the entries are acknowledged rolls the staged payload back and reports an unavailable outcome;
/// the operation's commit status is then unknown to the caller, who retries by identity.
/// </para>
/// <para>
/// Payloads are identified by the owning entry and are released only when no other live entry in the same tenant references the
/// same content hash, so deleting one logical reference cannot collect bytes another still needs, and no bytes are ever shared
/// across tenants.
/// </para>
/// </remarks>
internal abstract class ArtifactStateBackend: IArtifactStoreBackend, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _adapter;
    private bool _initialized;

    /// <summary>Initializes the shared gate and bookkeeping.</summary>
    /// <param name="adapter">The bounded adapter label used in logs.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentException"><paramref name="adapter"/> is blank.</exception>
    protected ArtifactStateBackend(string adapter, ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapter);
        _adapter = adapter;
        Logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Releases the operation gate.</summary>
    /// <remarks>Adapters that own additional resources release them by overriding <see cref="Dispose(bool)"/>.</remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Gets the authoritative projection, valid only while the gate is held.</summary>
    protected ArtifactStoreState State { get; } = new();

    /// <summary>Gets the content-free logger.</summary>
    protected ILogger Logger { get; }

    /// <inheritdoc/>
    public async ValueTask<ArtifactStorePrepareResult> PrepareAsync(ArtifactStorePrepareRequest request, DateTimeOffset now, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            "prepare",
            request.Grant.Authorization,
            lookup => ArtifactPlanner.PlanPrepare(lookup, request, now),
            static failure => new ArtifactStorePrepareRejected(failure),
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<ArtifactStoreFinalizeResult> FinalizeAsync(ArtifactStoreFinalizeRequest request, DateTimeOffset now, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            "finalize",
            request.Grant.Authorization,
            lookup => ArtifactPlanner.PlanFinalize(lookup, request, now),
            static failure => new ArtifactStoreFinalizeRejected(failure),
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<ArtifactStoreAbortResult> AbortAsync(ArtifactStoreAbortRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = now;
        return await ExecuteAsync(
            "abort",
            request.Grant.Authorization,
            lookup => ArtifactPlanner.PlanAbort(lookup, request),
            static failure => new ArtifactStoreAbortRejected(failure),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactStoreDeleteResult> DeleteAsync(ArtifactStoreDeleteRequest request, DateTimeOffset now, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            "delete",
            request.Grant.Authorization,
            lookup => ArtifactPlanner.PlanDelete(lookup, request, now),
            static failure => new ArtifactStoreDeleteRejected(failure),
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<ArtifactStoreReadResult> ReadAsync(ArtifactStoreReadRequest request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var authorization = request.Grant.Authorization;
            await EnsureInitializedAsync(authorization, cancellationToken).ConfigureAwait(false);
            var decision = ArtifactPlanner.PlanRead(State, request);
            if (decision.Rejection is { } rejection)
            {
                return rejection;
            }

            var entry = decision.Entry!;
            try
            {
                return await OpenPayloadAsync(authorization, entry, cancellationToken).ConfigureAwait(false) is { } payload
                    && payload.Length == entry.Metadata.DeclaredLength
                    && FileSecurityBinding.ContentFingerprint(payload) == entry.ContentHash
                    ? new ArtifactStoreReadOpened(entry.Reference!, new MemoryStream(payload, writable: false))
                    : new ArtifactStoreReadRejected(new ArtifactFailure(ArtifactFailureKind.Unavailable, "The stored artifact content is unavailable or failed verification."));
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            {
                ArtifactStoreObservation.Safe(() => ArtifactStoreLog.PersistFailed(Logger, _adapter, "read", exception.GetType().Name));
                return new ArtifactStoreReadRejected(new ArtifactFailure(ArtifactFailureKind.Unavailable, "The stored artifact content is unavailable."));
            }
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Releases the operation gate and any adapter-owned resources.</summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gate.Dispose();
        }
    }

    /// <summary>Loads every persisted entry into <see cref="State"/> exactly once, before the first operation.</summary>
    /// <param name="authorization">The captured authorization to recover under, or <see langword="null"/> when the backend's storage is not itself protected.</param>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <returns>A task completed once the projection reflects all acknowledged entries.</returns>
    /// <exception cref="InvalidOperationException">The durable state cannot be validated safely.</exception>
    internal async ValueTask InitializeAsync(SecurityAuthorizationContext? authorization, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync(authorization, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Recovers durable entries into <see cref="State"/>; called until it succeeds once, under the gate.</summary>
    /// <param name="authorization">The captured authorization of the operation that triggered recovery, or <see langword="null"/> when a host initializes the store explicitly at startup.</param>
    /// <param name="cancellationToken">Cancels before recovery completes.</param>
    /// <returns>A task completed once the projection is complete.</returns>
    protected abstract ValueTask RecoverAsync(SecurityAuthorizationContext? authorization, CancellationToken cancellationToken);

    /// <summary>Durably stages payload bytes so they survive until their owning entry is persisted.</summary>
    /// <param name="authorization">The captured authorization of the current operation, for backends whose own effects are protected.</param>
    /// <param name="entry">The entry that will own the payload.</param>
    /// <param name="content">The exact bytes.</param>
    /// <param name="cancellationToken">Cancels before staging commits.</param>
    /// <returns>A task completed once the bytes are durable.</returns>
    protected abstract ValueTask StagePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, ImmutableArray<byte> content, CancellationToken cancellationToken);

    /// <summary>Durably persists entries, which is the commit point of a plan.</summary>
    /// <param name="authorization">The captured authorization of the current operation, for backends whose own effects are protected.</param>
    /// <param name="upserts">The entries to insert or replace.</param>
    /// <param name="cancellationToken">Cancels before the entries are acknowledged.</param>
    /// <returns>A task completed once every entry is durable.</returns>
    protected abstract ValueTask PersistAsync(SecurityAuthorizationContext authorization, ImmutableArray<ArtifactEntry> upserts, CancellationToken cancellationToken);

    /// <summary>Removes a payload after its releasing entry was persisted.</summary>
    /// <param name="authorization">The captured authorization of the current operation, for backends whose own effects are protected.</param>
    /// <param name="entry">The entry whose payload is released.</param>
    /// <param name="stillReferenced">Whether another live entry in the same tenant references the same bytes, in which case they must be kept.</param>
    /// <param name="cancellationToken">Cancels best-effort release.</param>
    /// <returns>A task completed once the bytes are removed or retained.</returns>
    protected abstract ValueTask ReleasePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, bool stillReferenced, CancellationToken cancellationToken);

    /// <summary>Reads a committed payload, verifying it against the entry's integrity evidence.</summary>
    /// <param name="authorization">The captured authorization of the current operation, for backends whose own effects are protected.</param>
    /// <param name="entry">The live committed entry.</param>
    /// <param name="cancellationToken">Cancels before the bytes are returned.</param>
    /// <returns>The verified bytes, or <see langword="null"/> when the payload is missing or fails verification.</returns>
    protected abstract ValueTask<byte[]?> OpenPayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, CancellationToken cancellationToken);

    /// <summary>Determines whether an exception is an expected storage failure that maps to an unavailable outcome.</summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true"/> for input/output, access, encoding, and serialization failures.</returns>
    protected virtual bool IsStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException;

    private async ValueTask<TResult> ExecuteAsync<TResult>(
        string operation,
        SecurityAuthorizationContext authorization,
        Func<IArtifactEntryLookup, ArtifactPlan<TResult>> plan,
        Func<ArtifactFailure, TResult> unavailable,
        CancellationToken cancellationToken)
        where TResult : class
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync(authorization, cancellationToken).ConfigureAwait(false);
            var planned = plan(State);
            cancellationToken.ThrowIfCancellationRequested();
            if (planned.Upserts.IsEmpty && planned.Stage is null)
            {
                return planned.Result;
            }

            var staged = false;
            try
            {
                if (planned.Stage is { } stage)
                {
                    await StagePayloadAsync(authorization, stage.Entry, stage.Content, cancellationToken).ConfigureAwait(false);
                    staged = true;
                }

                await PersistAsync(authorization, planned.Upserts, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            {
                ArtifactStoreObservation.Safe(() => ArtifactStoreLog.PersistFailed(Logger, _adapter, operation, exception.GetType().Name));
                if (staged)
                {
                    await TryReleaseAsync(authorization, planned.Stage!.Entry, stillReferenced: State.HasLivePayload(planned.Stage.Entry.TenantId, planned.Stage.Entry.ContentHash)).ConfigureAwait(false);
                }

                return unavailable(new ArtifactFailure(ArtifactFailureKind.Unavailable, "The artifact could not be stored durably; its commit status is unknown."));
            }
            catch (OperationCanceledException) when (staged)
            {
                await TryReleaseAsync(authorization, planned.Stage!.Entry, stillReferenced: State.HasLivePayload(planned.Stage.Entry.TenantId, planned.Stage.Entry.ContentHash)).ConfigureAwait(false);
                throw;
            }

            State.Commit(planned.Upserts);
            foreach (var release in planned.Releases)
            {
                await TryReleaseAsync(authorization, release, State.HasLivePayload(release.TenantId, release.ContentHash)).ConfigureAwait(false);
            }

            return planned.Result;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async ValueTask EnsureInitializedAsync(SecurityAuthorizationContext? authorization, CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await RecoverAsync(authorization, cancellationToken).ConfigureAwait(false);
        _initialized = true;
    }

    private async ValueTask TryReleaseAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, bool stillReferenced)
    {
        try
        {
            await ReleasePayloadAsync(authorization, entry, stillReferenced, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            ArtifactStoreObservation.Safe(() => ArtifactStoreLog.ReleaseFailed(Logger, _adapter, exception.GetType().Name));
        }
    }
}
