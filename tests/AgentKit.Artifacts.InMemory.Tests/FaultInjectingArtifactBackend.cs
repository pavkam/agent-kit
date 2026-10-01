// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

/// <summary>Is an in-memory state backend whose storage steps can be made to fail, to prove the shared commit order and failure mapping.</summary>
internal sealed class FaultInjectingArtifactBackend(ILogger? logger = null): ArtifactStateBackend("faulty", logger)
{
    private readonly Dictionary<TenantArtifactPreparationKey, ImmutableArray<byte>> _payloads = [];

    internal Exception? RecoverFailure { get; set; }

    internal Exception? StageFailure { get; set; }

    internal Exception? PersistFailure { get; set; }

    internal Exception? ReleaseFailure { get; set; }

    internal Exception? OpenFailure { get; set; }

    internal bool PayloadMissing { get; set; }

    internal CancellationTokenSource? CancelDuringPersist { get; set; }

    internal int Recoveries { get; private set; }

    internal List<bool> Releases { get; } = [];

    internal int PayloadCount => _payloads.Count;

    protected override ValueTask RecoverAsync(SecurityAuthorizationContext? authorization, CancellationToken cancellationToken)
    {
        Recoveries++;
        return RecoverFailure is null ? ValueTask.CompletedTask : throw RecoverFailure;
    }

    protected override ValueTask StagePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, ImmutableArray<byte> content, CancellationToken cancellationToken)
    {
        if (StageFailure is not null)
        {
            throw StageFailure;
        }

        _payloads[entry.PreparationKey] = content;
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask PersistAsync(SecurityAuthorizationContext authorization, ImmutableArray<ArtifactEntry> upserts, CancellationToken cancellationToken)
    {
        if (CancelDuringPersist is { } source)
        {
            await source.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (PersistFailure is not null)
        {
            throw PersistFailure;
        }
    }

    protected override ValueTask ReleasePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, bool stillReferenced, CancellationToken cancellationToken)
    {
        Releases.Add(stillReferenced);
        if (ReleaseFailure is not null)
        {
            throw ReleaseFailure;
        }

        _ = _payloads.Remove(entry.PreparationKey);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<byte[]?> OpenPayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, CancellationToken cancellationToken)
    {
        return OpenFailure is not null
            ? throw OpenFailure
            : ValueTask.FromResult<byte[]?>(PayloadMissing || !_payloads.TryGetValue(entry.PreparationKey, out var content) ? null : [.. content]);
    }

    internal ValueTask InitializeForTestAsync(CancellationToken cancellationToken) => InitializeAsync(null, cancellationToken);
}
