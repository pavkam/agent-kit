// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

/// <summary>Keeps entries and payload bytes in process memory behind the shared state backend.</summary>
/// <remarks>Persistence is a no-op and recovery finds nothing, so all state is explicitly ephemeral. Payloads are keyed by tenant and preparation and never shared.</remarks>
internal sealed class InMemoryArtifactBackend(ILogger? logger): ArtifactStateBackend("in_memory", logger)
{
    private readonly Dictionary<TenantArtifactPreparationKey, ImmutableArray<byte>> _payloads = [];

    /// <inheritdoc/>
    protected override ValueTask RecoverAsync(SecurityAuthorizationContext? authorization, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    protected override ValueTask StagePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, ImmutableArray<byte> content, CancellationToken cancellationToken)
    {
        _payloads[entry.PreparationKey] = content;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask PersistAsync(SecurityAuthorizationContext authorization, ImmutableArray<ArtifactEntry> upserts, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    protected override ValueTask ReleasePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, bool stillReferenced, CancellationToken cancellationToken)
    {
        _ = _payloads.Remove(entry.PreparationKey);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask<byte[]?> OpenPayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, CancellationToken cancellationToken) =>
        ValueTask.FromResult<byte[]?>(_payloads.TryGetValue(entry.PreparationKey, out var content) ? [.. content] : null);
}
