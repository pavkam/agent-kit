// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Validates, authorizes, and delegates durable-memory proposals, corrections, and deletions.</summary>
/// <remarks>
/// <para>
/// Each operation asks the profile runtime selector for the exact profile key and version in its context, holds the lease through
/// policy, authorization, storage, and observation, and disposes it. A proposal or correction is written only after the policy
/// dispatcher explicitly allows it and the security authority the context's authorization names issues a single-use grant bound
/// to the exact store operation. A denial, an unavailable authority, a missing store, or an excessive classification returns a
/// typed refusal before any store is contacted.
/// </para>
/// <para>
/// Deletion commits an authoritative tombstone before it purges. Events are published after the store committed; a required sink
/// that cannot record them is logged and never undoes the committed change, and retrying the same command replays the committed result.
/// </para>
/// </remarks>
internal sealed class DefaultMemoryCoordinator: IMemoryCoordinator
{
    private readonly IMemoryProfileRuntimeSelector _runtimes;
    private readonly IMemoryPolicyDispatcher _policies;
    private readonly MemoryGrantIssuer _grants;
    private readonly TimeProvider _time;
    private readonly ILogger<DefaultMemoryCoordinator> _logger;

    /// <summary>Initializes the coordinator.</summary>
    /// <param name="runtimes">The selector that activates the exact profile runtime for each operation.</param>
    /// <param name="policies">The dispatcher that combines the profile's policies.</param>
    /// <param name="grants">The issuer of single-use grants from the captured authority.</param>
    /// <param name="time">The injected clock for record instants and observation.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultMemoryCoordinator(
        IMemoryProfileRuntimeSelector runtimes,
        IMemoryPolicyDispatcher policies,
        MemoryGrantIssuer grants,
        TimeProvider time,
        ILogger<DefaultMemoryCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(runtimes);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(time);
        _runtimes = runtimes;
        _policies = policies;
        _grants = grants;
        _time = time;
        _logger = logger ?? NullLogger<DefaultMemoryCoordinator>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<MemoryProposalResult> ProposeAsync(MemoryProposal proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return await RunAsync(
            "propose", AgentKitActivityNames.MemoryPropose, proposal.Id, proposal.Context,
            async (lease, _) => await ProposeCoreAsync(proposal, lease, cancellationToken).ConfigureAwait(false),
            static result => result.Outcome == MemoryProposalOutcome.Accepted ? null : OutcomeName(result),
            static failure => MemoryProposalResult.Rejected(failure),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<MemoryTransitionResult> CorrectAsync(MemoryCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await RunAsync(
            "correct", AgentKitActivityNames.MemoryCorrect, request.Id, request.Context,
            async (lease, _) => await CorrectCoreAsync(request, lease, cancellationToken).ConfigureAwait(false),
            static result => result.Failure is { } failure ? MemoryStoreObservationName(failure.Kind) : null,
            static failure => MemoryTransitionResult.Rejected(failure),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<MemoryDeleteResult> DeleteAsync(MemoryDeleteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await RunAsync(
            "delete", AgentKitActivityNames.MemoryDelete, command.Id, command.Context,
            async (lease, _) => await DeleteCoreAsync(command, lease, cancellationToken).ConfigureAwait(false),
            static result => result.Failure is { } failure ? MemoryStoreObservationName(failure.Kind) : null,
            static failure => MemoryDeleteResult.Rejected(failure),
            cancellationToken).ConfigureAwait(false);
    }

    private static string OutcomeName(MemoryProposalResult result) => result.Outcome switch
    {
        MemoryProposalOutcome.Accepted => "completed",
        MemoryProposalOutcome.PolicyDenied => "policy_denied",
        MemoryProposalOutcome.Rejected => result.Failure is { } failure ? MemoryStoreObservationName(failure.Kind) : "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "The proposal outcome is undefined."),
    };

    private static string MemoryStoreObservationName(MemoryStoreFailureKind kind) => kind switch
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
        MemoryId memoryId,
        MemoryOperationContext context,
        Func<IMemoryProfileRuntimeLease, CancellationToken, ValueTask<TResult>> work,
        Func<TResult, string?> refusalOf,
        Func<MemoryStoreFailure, TResult> unavailable,
        CancellationToken cancellationToken)
    {
        var started = MemoryObservation.TryTimestamp(_time);
        using var scope = AgentKitActivityScope.Start(
            activityName,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.MemoryId, memoryId.ToString()),
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
                return Finish(scope, operation, memoryId, started, unavailable(failure), "unavailable");
            }

            await using var lease = selected.Runtime;
            var result = await work(lease, cancellationToken).ConfigureAwait(false);
            return Finish(scope, operation, memoryId, started, result, refusalOf(result));
        }
        catch (OperationCanceledException)
        {
            MemoryObservation.Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            MemoryObservation.Safe(() => MemoryLog.OperationCancelled(_logger, operation, memoryId));
            MemoryObservation.Safe(() => MemoryObservation.RecordOperation(operation, "cancelled", MemoryObservation.TryElapsed(_time, started)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            MemoryObservation.Safe(() => scope.Activity.SetFailed("faulted", errorType));
            MemoryObservation.Safe(() => MemoryLog.OperationFaulted(_logger, operation, memoryId, errorType));
            MemoryObservation.Safe(() => MemoryObservation.RecordOperation(operation, "faulted", MemoryObservation.TryElapsed(_time, started)));
            throw;
        }
    }

    private TResult Finish<TResult>(AgentKitActivityScope scope, string operation, MemoryId memoryId, long? started, TResult result, string? refusal)
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
        MemoryObservation.Safe(() => MemoryLog.OperationCompleted(_logger, refusal is null ? LogLevel.Information : LogLevel.Warning, operation, outcome, memoryId));
        MemoryObservation.Safe(() => MemoryObservation.RecordOperation(operation, outcome, MemoryObservation.TryElapsed(_time, started)));
        return result;
    }

    private async ValueTask<MemoryProposalResult> ProposeCoreAsync(MemoryProposal proposal, IMemoryProfileRuntimeLease lease, CancellationToken cancellationToken)
    {
        var profile = lease.Profile;
        if (!profile.DurableMemoryEnabled || lease.MemoryStore is not { } store)
        {
            return MemoryProposalResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.Unavailable, "Durable memory is not enabled for the memory profile."));
        }

        if (proposal.Classification > profile.MaximumClassification)
        {
            return await DenyAsync(lease, proposal, new MemoryPolicyDenied(
                new ComponentId("agentkit.memory.classification-ceiling"), "classification-exceeded", "The proposal's classification exceeds the profile's ceiling."), cancellationToken).ConfigureAwait(false);
        }

        var decision = await _policies.EvaluateAsync(proposal, new MemoryPolicyContext(profile, _time.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        if (decision is not MemoryPolicyAllowed)
        {
            return await DenyAsync(
                lease, proposal,
                decision as MemoryPolicyDenied ?? new MemoryPolicyDenied(FailClosedMemoryPolicy.Id, "policy-invalid", "The memory policy returned an unrecognized decision."),
                cancellationToken).ConfigureAwait(false);
        }

        var now = _time.GetUtcNow();
        var runId = (proposal.Context.Correlation as InRunOperationCorrelation)?.RunId ?? proposal.Provenance.SourceRunId;
        if (runId is not { } sourceRun)
        {
            return MemoryProposalResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.ScopeMismatch, "The proposal names no source run."));
        }

        var identity = proposal.Context.Identity;
        var record = new DurableMemoryRecord(
            proposal.Id,
            proposal.Context.AgentId,
            proposal.Context.SessionId,
            sourceRun,
            proposal.Namespace,
            identity.TenantId,
            new PrincipalVisibility(identity.TenantId, identity.PrincipalId, proposal.ShareWithTenant),
            proposal.Kind,
            proposal.Content,
            proposal.Classification,
            proposal.Provenance,
            proposal.Retention,
            MemoryLifecycleState.Active,
            new VersionToken("1"),
            now,
            now,
            proposal.Extensions);
        var key = new IdempotencyKey($"agentkit.memory.propose:{proposal.Id}");
        var issue = await _grants.IssueAsync(
            lease.SecurityAuthorities, proposal.Context.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Create,
            [MemorySecurityBinding.Resource(record.Id)], MemorySecurityBinding.WriteFingerprint(record, key), cancellationToken).ConfigureAwait(false);
        if (issue.Grant is null)
        {
            return MemoryProposalResult.Rejected(issue.ToFailure());
        }

        var written = await store.WriteAsync(new MemoryWriteRequest(record, key, issue.Grant), cancellationToken).ConfigureAwait(false);
        if (!written.IsWritten)
        {
            return MemoryProposalResult.Rejected(written.Failure);
        }

        if (!written.Replayed)
        {
            await PublishAsync(lease, proposal.Context, MemoryEventKind.ProposalAccepted, record.Id, "accepted", "propose", cancellationToken).ConfigureAwait(false);
        }

        return MemoryProposalResult.Accepted(written.Record, written.Replayed);
    }

    private async ValueTask<MemoryProposalResult> DenyAsync(IMemoryProfileRuntimeLease lease, MemoryProposal proposal, MemoryPolicyDenied denial, CancellationToken cancellationToken)
    {
        await PublishAsync(lease, proposal.Context, MemoryEventKind.ProposalDenied, proposal.Id, denial.Code, "propose", cancellationToken).ConfigureAwait(false);
        return MemoryProposalResult.PolicyDenied(denial);
    }

    private async ValueTask<MemoryTransitionResult> CorrectCoreAsync(MemoryCorrectionRequest request, IMemoryProfileRuntimeLease lease, CancellationToken cancellationToken)
    {
        var profile = lease.Profile;
        if (!profile.DurableMemoryEnabled || lease.MemoryStore is not { } store)
        {
            return MemoryTransitionResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.Unavailable, "Durable memory is not enabled for the memory profile."));
        }

        var context = request.Context;
        var readKey = MemorySecurityBinding.ReadFingerprint(request.Id);
        var readIssue = await _grants.IssueAsync(
            lease.SecurityAuthorities, context.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            [MemorySecurityBinding.Resource(request.Id)], readKey, cancellationToken).ConfigureAwait(false);
        if (readIssue.Grant is null)
        {
            return MemoryTransitionResult.Rejected(readIssue.ToFailure());
        }

        var existing = await store.ReadAsync(new MemoryReadRequest(request.Id, readIssue.Grant), cancellationToken).ConfigureAwait(false);
        if (!existing.IsFound)
        {
            return MemoryTransitionResult.Rejected(existing.Failure ?? new MemoryStoreFailure(MemoryStoreFailureKind.InvalidTransition, "The memory is deleted."));
        }

        var original = existing.Record;
        if (original.Version != request.ExpectedVersion)
        {
            return MemoryTransitionResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.VersionConflict, "The expected version does not match the stored record."));
        }

        var now = _time.GetUtcNow();
        var proposal = new MemoryProposal(
            request.ReplacementId, context, original.Kind, request.Content, original.Classification, request.Provenance, original.Retention, now, original.Extensions)
        {
            Namespace = original.Namespace,
            ShareWithTenant = original.Visibility.SharedWithTenant,
        };
        if (proposal.Classification > profile.MaximumClassification)
        {
            return MemoryTransitionResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.Denied, "The corrected memory's classification exceeds the profile's ceiling."));
        }

        var decision = await _policies.EvaluateAsync(proposal, new MemoryPolicyContext(profile, now), cancellationToken).ConfigureAwait(false);
        if (decision is not MemoryPolicyAllowed)
        {
            var denied = decision as MemoryPolicyDenied;
            await PublishAsync(lease, context, MemoryEventKind.ProposalDenied, request.ReplacementId, denied?.Code ?? "policy-invalid", "correct", cancellationToken).ConfigureAwait(false);
            return MemoryTransitionResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.Denied, denied?.SafeMessage ?? "The memory policy refused the correction."));
        }

        var replacement = new DurableMemoryRecord(
            request.ReplacementId, original.AgentId, context.SessionId, (context.Correlation as InRunOperationCorrelation)?.RunId ?? original.SourceRunId,
            original.Namespace, original.TenantId, original.Visibility, original.Kind, request.Content, original.Classification, request.Provenance,
            original.Retention, MemoryLifecycleState.Active, new VersionToken("1"), now, now, original.Extensions);
        var transitionKey = new IdempotencyKey($"agentkit.memory.correct:{request.IdempotencyKey.Value}");
        var fingerprint = MemorySecurityBinding.TransitionFingerprint(request.Id, MemoryLifecycleState.Corrected, request.ExpectedVersion, replacement, transitionKey, now);
        var issue = await _grants.IssueAsync(
            lease.SecurityAuthorities, context.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
            [MemorySecurityBinding.Resource(request.Id)], fingerprint, cancellationToken).ConfigureAwait(false);
        if (issue.Grant is null)
        {
            return MemoryTransitionResult.Rejected(issue.ToFailure());
        }

        var result = await store.TransitionAsync(
            new MemoryTransitionRequest(request.Id, MemoryLifecycleState.Corrected, request.ExpectedVersion, replacement, transitionKey, now, issue.Grant),
            cancellationToken).ConfigureAwait(false);
        if (result.IsTransitioned && !result.Replayed)
        {
            await PublishAsync(lease, context, MemoryEventKind.MemoryCorrected, request.Id, "corrected", "correct", cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async ValueTask<MemoryDeleteResult> DeleteCoreAsync(MemoryDeleteCommand command, IMemoryProfileRuntimeLease lease, CancellationToken cancellationToken)
    {
        if (!lease.Profile.DurableMemoryEnabled || lease.MemoryStore is not { } store)
        {
            return MemoryDeleteResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.Unavailable, "Durable memory is not enabled for the memory profile."));
        }

        var tombstone = await DeleteStepAsync(command, lease, store, MemoryDeleteMode.Tombstone, command.ExpectedVersion, "tombstone", cancellationToken).ConfigureAwait(false);
        if (!tombstone.IsDeleted)
        {
            return tombstone;
        }

        if (!tombstone.Replayed)
        {
            await PublishAsync(lease, command.Context, MemoryEventKind.MemoryDeleted, command.Id, "tombstoned", "delete", cancellationToken).ConfigureAwait(false);
        }

        if (command.Mode != MemoryDeleteMode.Purge || tombstone.Receipt.PhysicallyPurged)
        {
            return tombstone;
        }

        var purge = await DeleteStepAsync(command, lease, store, MemoryDeleteMode.Purge, null, "purge", cancellationToken).ConfigureAwait(false);
        if (!purge.IsDeleted)
        {
            return tombstone;
        }

        if (!purge.Replayed)
        {
            await PublishAsync(lease, command.Context, MemoryEventKind.MemoryPurged, command.Id, "purged", "delete", cancellationToken).ConfigureAwait(false);
        }

        return purge;
    }

    private async ValueTask<MemoryDeleteResult> DeleteStepAsync(
        MemoryDeleteCommand command,
        IMemoryProfileRuntimeLease lease,
        IMemoryStore store,
        MemoryDeleteMode mode,
        VersionToken? expected,
        string step,
        CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var key = new IdempotencyKey($"agentkit.memory.delete:{command.IdempotencyKey.Value}:{step}");
        var issue = await _grants.IssueAsync(
            lease.SecurityAuthorities, command.Context.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
            [MemorySecurityBinding.Resource(command.Id)], MemorySecurityBinding.DeleteFingerprint(command.Id, expected, mode, key, now), cancellationToken).ConfigureAwait(false);
        return issue.Grant is null
            ? MemoryDeleteResult.Rejected(issue.ToFailure())
            : await store.DeleteAsync(new MemoryDeleteRequest(command.Id, expected, mode, key, now, issue.Grant), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask PublishAsync(
        IMemoryProfileRuntimeLease lease,
        MemoryOperationContext context,
        MemoryEventKind kind,
        MemoryId memoryId,
        string outcome,
        string operation,
        CancellationToken cancellationToken)
    {
        var dispatch = await lease.Events.PublishAsync(
            lease.Profile.Key,
            new MemoryEvent(
                kind, context.Identity.TenantId, context.AgentId, context.SessionId, lease.Profile.Key, lease.Profile.Version, memoryId, null, null, outcome, null, _time.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        if (!dispatch.RequiredDeliveryComplete)
        {
            MemoryObservation.Safe(() => MemoryLog.RequiredObservationMissing(_logger, operation, memoryId));
        }
    }
}
