// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

/// <summary>Stores staged and committed artifact bytes in tenant-partitioned process memory for deterministic tests.</summary>
/// <remarks>
/// Content survives only for the lifetime of this store. Staging is never readable as committed content, every operation consumes
/// an exact single-use grant before state access, committed bytes are returned by copy, and replay keys cannot silently change
/// prepare content or policy. The adapter shares one planner with the durable adapters, so its lifecycle semantics are identical.
/// The instance is thread-safe.
/// </remarks>
public sealed class InMemoryArtifactStore: IArtifactStore
{
    private readonly ArtifactStoreGateway _gateway;

    /// <summary>Initializes the store over the authoritative grant store and deterministic clock.</summary>
    /// <param name="grants">The atomic single-use grant store.</param>
    /// <param name="time">The clock used for expiry and publication evidence.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public InMemoryArtifactStore(ISecurityGrantStore grants, TimeProvider time)
        : this(grants, time, new GuidEnforcementIntentIdGenerator())
    {
    }

    /// <summary>Initializes the store with a replaceable source of fresh atomic enforcement-intent identities.</summary>
    /// <param name="grants">The non-null authoritative store that atomically consumes a grant and retains permission to begin.</param>
    /// <param name="time">The non-null clock used for expiry and publication evidence.</param>
    /// <param name="intentIds">The non-null thread-safe source of unique per-operation enforcement intent identities.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grants"/>, <paramref name="time"/>, or <paramref name="intentIds"/> is null.</exception>
    public InMemoryArtifactStore(
        ISecurityGrantStore grants,
        TimeProvider time,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        ILogger<InMemoryArtifactStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(intentIds);
        _gateway = new ArtifactStoreGateway(
            Adapter, SecurityAudienceId, new InMemoryArtifactBackend(logger), grants, intentIds, time, logger);
    }

    private static string Adapter => "in_memory";

    private static ComponentId SecurityAudienceId { get; } = new("agentkit.artifacts.in-memory");

    /// <inheritdoc/>
    public ComponentId SecurityAudience => _gateway.SecurityAudience;

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
}
