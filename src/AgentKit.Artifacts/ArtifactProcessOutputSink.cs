// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Preserves complete truncated process streams through one keyed artifact coordinator.</summary>
/// <remarks>The sink is bound to the coordinator it is constructed over. It never selects a coordinator or profile itself and aborts staging when publication fails.</remarks>
public sealed class ArtifactProcessOutputSink: IProcessOutputArtifactSink
{
    private readonly IArtifactCoordinator _artifacts;
    private readonly ArtifactDirectoryId _directory;
    private readonly AgentArtifactOptionsSnapshot _options;

    /// <summary>Initializes the process-output adapter over one coordinator and captured policy.</summary>
    /// <param name="artifacts">The protected artifact coordinator this sink is bound to.</param>
    /// <param name="defaultDirectory">The profile's default directory, used when the options name none.</param>
    /// <param name="options">The captured artifact and process-output policy.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="defaultDirectory"/> is blank.</exception>
    internal ArtifactProcessOutputSink(IArtifactCoordinator artifacts, ArtifactDirectoryId defaultDirectory, AgentArtifactOptionsSnapshot options)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultDirectory.Value, nameof(defaultDirectory));
        ArgumentNullException.ThrowIfNull(options);
        _artifacts = artifacts;
        _directory = options.ProcessOutputDirectory ?? defaultDirectory;
        _options = options;
    }

    /// <inheritdoc/>
    public async Task<ProcessOutputArtifactResult> StoreAsync(ProcessOutputArtifactRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var contentHash = FileSecurityBinding.ContentFingerprint(request.Content.AsSpan());
        var metadata = new ArtifactMetadata(
            Owner(request.Scope),
            "application/octet-stream",
            request.Content.Length,
            contentHash,
            _options.ProcessOutputClassification,
            request.Scope.SessionId.HasValue ? ArtifactOwnershipKind.Session : ArtifactOwnershipKind.Run,
            ArtifactMutability.Immutable,
            new ArtifactRetention(_options.ProcessOutputRetentionPolicy, null, false),
            null);
        using var content = new MemoryStream([.. request.Content], writable: false);
        var prepare = await _artifacts.PrepareAsync(new ArtifactPrepareRequest(
            request.Scope.AgentId,
            request.Scope.SessionId,
            null,
            request.Scope.Correlation,
            request.Authorization,
            _directory,
            metadata,
            content,
            Suffix(request.IdempotencyKey, "prepare")), cancellationToken).ConfigureAwait(false);
        if (prepare is not ArtifactPrepared prepared)
        {
            return new ProcessOutputArtifactRejected(
                prepare is ArtifactPrepareRejected rejected ? rejected.Failure.SafeMessage : "Complete process output could not be staged.");
        }

        var finalized = await _artifacts.FinalizeAsync(new ArtifactFinalizeRequest(
            prepared.PreparationId,
            request.Scope.AgentId,
            request.Scope.SessionId,
            null,
            request.Scope.Correlation,
            request.Authorization,
            Suffix(request.IdempotencyKey, "finalize")), cancellationToken).ConfigureAwait(false);
        if (finalized is ArtifactFinalized committed)
        {
            return new ProcessOutputArtifactStored(committed.Reference);
        }

        _ = await _artifacts.AbortAsync(new ArtifactAbortRequest(
            prepared.PreparationId,
            request.Scope.AgentId,
            request.Scope.SessionId,
            request.Scope.Correlation,
            request.Authorization,
            ArtifactAbortReason.ReferenceCommitFailure,
            Suffix(request.IdempotencyKey, "abort")), CancellationToken.None).ConfigureAwait(false);
        return new ProcessOutputArtifactRejected(
            finalized is ArtifactFinalizeRejected finalizeRejected ? finalizeRejected.Failure.SafeMessage : "Complete process output could not be published.");
    }

    private static ArtifactOwnerId Owner(SecurityAuthorizationScope scope) => scope.SessionId is { } sessionId
        ? new ArtifactOwnerId($"session:{sessionId}")
        : scope.Correlation is InRunOperationCorrelation inRun
            ? new ArtifactOwnerId($"run:{inRun.RunId}")
            : new ArtifactOwnerId($"operation:{scope.Correlation.OperationId}");

    private static IdempotencyKey Suffix(IdempotencyKey key, string suffix) => new($"{key.Value}:{suffix}");
}
