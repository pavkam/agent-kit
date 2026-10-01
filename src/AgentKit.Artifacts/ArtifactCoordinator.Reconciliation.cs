// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

internal sealed partial class ArtifactCoordinator
{
    /// <inheritdoc/>
    public ValueTask<ArtifactReconciliationResult> ReconcileAsync(ArtifactReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ValueTask<ArtifactReconciliationResult>(ArtifactObservability.ObserveAsync(
            Target(AgentKitActivityNames.ArtifactReconcile, "reconcile", request.Authorization.Identity.TenantId, preparationId: request.PreparationId),
            () => ReconcileCoreAsync(request, cancellationToken),
            static result => result switch
            {
                ArtifactReconciled => ("reconciled", true),
                ArtifactReconciliationPending => ("pending", true),
                ArtifactReconciliationRejected rejected => (ArtifactObservability.Name(rejected.Failure.Kind), false),
                _ => ("unknown", false),
            }));
    }

    private async Task<ArtifactReconciliationResult> ReconcileCoreAsync(ArtifactReconciliationRequest request, CancellationToken cancellationToken)
    {
        var tenant = request.Authorization.Identity.TenantId;
        if (_intents is null)
        {
            return Pending(request, ArtifactReconciliationPendingReason.EvidenceUnavailable);
        }

        var read = await _intents.GetAsync(tenant, request.PreparationId, cancellationToken).ConfigureAwait(false);
        if (read.Intent is not { } intent)
        {
            return Pending(request, ArtifactReconciliationPendingReason.EvidenceUnavailable);
        }

        switch (intent.State)
        {
            case ArtifactReferenceCommitState.Committed:
                return await CompleteAsync(request, ArtifactReconciliationDisposition.ReferenceCommitted, cancellationToken).ConfigureAwait(false);
            case ArtifactReferenceCommitState.Collected:
                return new ArtifactReconciled(ArtifactReconciliationDisposition.AlreadyCollected);
            case ArtifactReferenceCommitState.Pending:
                var now = _time.GetUtcNow();
                var notBefore = intent.RecordedAt + _options.OrphanRetention;
                if (intent.Pin.HeldUntil > notBefore)
                {
                    notBefore = intent.Pin.HeldUntil;
                }

                if (now < notBefore)
                {
                    return Pending(request, ArtifactReconciliationPendingReason.RetentionWindowOpen);
                }

                // Fence first: from here a late reference commit loses the conditional transition, so the terminal disposition is established.
                var fence = await _intents.TransitionAsync(
                    tenant, request.PreparationId, ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, now, cancellationToken).ConfigureAwait(false);
                return fence.Intent?.State switch
                {
                    ArtifactReferenceCommitState.Committed => await CompleteAsync(request, ArtifactReconciliationDisposition.ReferenceCommitted, cancellationToken).ConfigureAwait(false),
                    ArtifactReferenceCommitState.Collected => new ArtifactReconciled(ArtifactReconciliationDisposition.AlreadyCollected),
                    ArtifactReferenceCommitState.Fenced => await CollectAsync(request, cancellationToken).ConfigureAwait(false),
                    ArtifactReferenceCommitState.Pending => Pending(request, ArtifactReconciliationPendingReason.EvidenceUnavailable),
                    null => Pending(request, ArtifactReconciliationPendingReason.EvidenceUnavailable),
                    _ => throw new UnreachableException(),
                };
            case ArtifactReferenceCommitState.Fenced:
                return await CollectAsync(request, cancellationToken).ConfigureAwait(false);
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Removes a fenced, terminally uncommitted object, or conservatively retains it.</summary>
    private async Task<ArtifactReconciliationResult> CollectAsync(ArtifactReconciliationRequest request, CancellationToken cancellationToken)
    {
        var abort = await AbortAsync(
            new ArtifactAbortRequest(
                request.PreparationId, request.AgentId, request.SessionId, request.Correlation, request.Authorization,
                ArtifactAbortReason.Abandoned, Derive(request.IdempotencyKey, "abort")),
            cancellationToken).ConfigureAwait(false);
        return abort switch
        {
            ArtifactAborted => await CompleteAsync(request, ArtifactReconciliationDisposition.Collected, cancellationToken).ConfigureAwait(false),
            ArtifactAbortRejected { Failure.Kind: ArtifactFailureKind.Conflict } => await CollectFinalizedAsync(request, cancellationToken).ConfigureAwait(false),
            // No consulted backend holds the preparation; only an exhaustive answer proves the object is gone.
            ArtifactAbortRejected { Failure.Kind: ArtifactFailureKind.NotFound } => await AllBackendsAnswerAsync(cancellationToken).ConfigureAwait(false)
                ? await CompleteAsync(request, ArtifactReconciliationDisposition.Collected, cancellationToken).ConfigureAwait(false)
                : Pending(request, ArtifactReconciliationPendingReason.EvidenceUnavailable),
            ArtifactAbortRejected { Failure.Kind: ArtifactFailureKind.Unavailable } => Pending(request, ArtifactReconciliationPendingReason.EvidenceUnavailable),
            ArtifactAbortRejected rejected => new ArtifactReconciliationRejected(rejected.Failure),
            _ => Pending(request, ArtifactReconciliationPendingReason.EvidenceUnavailable),
        };
    }

    /// <summary>Deletes content that finalized before the intent was fenced, through the ordinary retention and authorization path.</summary>
    private async Task<ArtifactReconciliationResult> CollectFinalizedAsync(ArtifactReconciliationRequest request, CancellationToken cancellationToken)
    {
        // The preparation is already finalized, so replaying its finalize returns the same reference and publishes nothing new.
        var finalized = await FinalizeAsync(
            new ArtifactFinalizeRequest(
                request.PreparationId, request.AgentId, request.SessionId, null, request.Correlation, request.Authorization,
                Derive(request.IdempotencyKey, "finalize")),
            cancellationToken).ConfigureAwait(false);
        if (finalized is not ArtifactFinalized { Reference: var reference })
        {
            return finalized is ArtifactFinalizeRejected { Failure.Kind: ArtifactFailureKind.NotFound }
                ? await CompleteAsync(request, ArtifactReconciliationDisposition.Collected, cancellationToken).ConfigureAwait(false)
                : Pending(request, ArtifactReconciliationPendingReason.EvidenceUnavailable);
        }

        var deleted = await DeleteAsync(
            new ArtifactDeleteRequest(
                request.AgentId, request.SessionId, null, request.Correlation, request.Authorization, reference,
                Derive(request.IdempotencyKey, "delete")),
            cancellationToken).ConfigureAwait(false);
        return deleted switch
        {
            ArtifactDeleted => await CompleteAsync(request, ArtifactReconciliationDisposition.Collected, cancellationToken).ConfigureAwait(false),
            ArtifactDeleteRejected { Failure.Kind: ArtifactFailureKind.RetentionConflict } => Pending(request, ArtifactReconciliationPendingReason.RetentionHold),
            ArtifactDeleteRejected { Failure.Kind: ArtifactFailureKind.Denied } rejected => new ArtifactReconciliationRejected(rejected.Failure),
            _ => Pending(request, ArtifactReconciliationPendingReason.EvidenceUnavailable),
        };
    }

    /// <summary>Records the terminal disposition on the intent, when one exists to complete, and publishes the reconciliation event.</summary>
    private async Task<ArtifactReconciliationResult> CompleteAsync(
        ArtifactReconciliationRequest request,
        ArtifactReconciliationDisposition disposition,
        CancellationToken cancellationToken)
    {
        var tenant = request.Authorization.Identity.TenantId;
        if (disposition == ArtifactReconciliationDisposition.Collected && _intents is not null)
        {
            // Losing this transition is harmless: the object is already gone and the intent stays fenced for a later replay.
            _ = await _intents.TransitionAsync(
                tenant, request.PreparationId, ArtifactReferenceCommitState.Fenced, ArtifactReferenceCommitState.Collected,
                _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }

        await PublishAsync(
            new ArtifactReconciledEvent(_key, tenant, _profile.Key, _time.GetUtcNow(), request.PreparationId, disposition),
            cancellationToken).ConfigureAwait(false);
        return new ArtifactReconciled(disposition);
    }

    private ArtifactReconciliationPending Pending(ArtifactReconciliationRequest request, ArtifactReconciliationPendingReason reason)
    {
        var preparation = request.PreparationId.ToString();
        var reasonText = reason.ToString();
        ArtifactObservability.Safe(() => ArtifactLog.ReconciliationPending(_logger, _key.Value, preparation, reasonText));
        return new ArtifactReconciliationPending(reason);
    }

    private async ValueTask<bool> AllBackendsAnswerAsync(CancellationToken cancellationToken)
    {
        foreach (var backend in Backends())
        {
            if (await _stores.SelectBackendAsync(backend, cancellationToken).ConfigureAwait(false) is not ArtifactStoreSelected)
            {
                return false;
            }
        }

        return true;
    }

    private static IdempotencyKey Derive(IdempotencyKey key, string suffix) => new($"{key.Value}:{suffix}");
}
