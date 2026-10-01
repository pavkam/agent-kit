// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json;

/// <summary>Stores artifact entries in a flushed newline-delimited JSON log and payloads as content-addressed files under one fixed local root.</summary>
/// <remarks>
/// <para>
/// Every acknowledged mutation appends one flushed log record holding the complete entries it changed, after the payload file it
/// needs was written atomically, so an acknowledged finalize, abort, or tombstone survives process loss. Live state is projected
/// during initialization by replaying the log through the same planner the other adapters run; a torn trailing append is recovered
/// or refused according to the target's recovery mode. A tombstone outlives replay, so a restored log can never resurrect a
/// deleted version.
/// </para>
/// <para>
/// The adapter holds an advisory exclusive lock on its root and rejects a second writer, so it claims no multi-process
/// coordination, and it inherits no atomicity with session history. Every operation consumes a single-use grant that binds that
/// exact operation before any state is read or written. The instance is thread-safe; dispose it to release the root.
/// </para>
/// </remarks>
public sealed class JsonArtifactStore: IArtifactStore, IDisposable
{
    private readonly ArtifactStoreGateway _gateway;
    private readonly JsonArtifactBackend _backend;

    /// <summary>Initializes a store bound to one host-authorized root without opening, creating, or locking it.</summary>
    /// <param name="target">The non-null exact root and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable bounds and encoding contract.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock used for expiry, publication evidence, and observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public JsonArtifactStore(
        JsonArtifactTarget target,
        JsonArtifactSettings settings,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger<JsonArtifactStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        var resolvedLogger = (ILogger?) logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _backend = new JsonArtifactBackend(new JsonArtifactFile(target, settings, JsonArtifactFileKind.Store, resolvedLogger), resolvedLogger);
        _gateway = new ArtifactStoreGateway("json", new ComponentId("agentkit.artifacts.json"), _backend, grants, intentIds, time, resolvedLogger);
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience => _gateway.SecurityAudience;

    /// <summary>Validates or creates the root, binds its encoding contract, and replays recorded entries into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, identity, encoding contract, or persisted evidence cannot be validated safely, or a second writer holds the advisory lock.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Calling it during trusted host startup surfaces a misconfigured root at boot. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => _backend.InitializeAsync(null, cancellationToken);

    /// <inheritdoc/>
    public Task<ArtifactStorePrepareResult> PrepareAsync(ArtifactStorePrepareRequest request, CancellationToken cancellationToken = default) =>
        _gateway.PrepareAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ArtifactStoreFinalizeResult> FinalizeAsync(ArtifactStoreFinalizeRequest request, CancellationToken cancellationToken = default) =>
        _gateway.FinalizeAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ArtifactStoreAbortResult> AbortAsync(ArtifactStoreAbortRequest request, CancellationToken cancellationToken = default) =>
        _gateway.AbortAsync(request, cancellationToken);

    /// <inheritdoc/>
    public Task<ArtifactStoreReadResult> ReadAsync(ArtifactStoreReadRequest request, CancellationToken cancellationToken = default) =>
        _gateway.ReadAsync(request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ArtifactStoreDeleteResult> DeleteAsync(ArtifactStoreDeleteRequest request, CancellationToken cancellationToken = default) =>
        _gateway.DeleteAsync(request, cancellationToken);

    /// <summary>Releases the advisory lock so another writer may open the root.</summary>
    /// <remarks>Disposal is idempotent. Acknowledged records were already flushed and are unaffected.</remarks>
    public void Dispose() => _backend.Dispose();
}
