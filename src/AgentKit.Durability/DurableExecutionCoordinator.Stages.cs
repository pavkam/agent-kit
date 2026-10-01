// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Implements the individual coordinator stages shared by execution and recovery.</summary>
internal sealed partial class DurableExecutionCoordinator
{
    private async ValueTask<IDurabilityRuntimeLease> ActivateAsync(
        AgentKitActivityScope scope,
        DurableExecutionContext context,
        long started,
        CancellationToken cancellationToken)
    {
        Debug.Assert(context is not null, "Public entry points validate the captured context.");
        var activation = await _runtimeSelector.ActivateAsync(context, cancellationToken).ConfigureAwait(false);
        if (activation is DurabilityRuntimeActivated activated)
        {
            LogCompleted(DurableCoordinatorStage.Activate, DurableCoordinatorOutcome.Completed);
            return activated.Lease;
        }

        var reason = activation is DurabilityRuntimeActivationFailed failed
            ? failed.SafeMessage
            : "The captured durability runtime could not be activated.";
        throw Fail(scope, DurableCoordinatorStage.Activate, DurableCoordinatorOutcome.Unavailable, started, reason);
    }

    private async ValueTask<IExecutionLease> AcquireAsync(
        AgentKitActivityScope scope,
        IDurabilityRuntimeLease runtime,
        DurableOperationAddress address,
        long started,
        CancellationToken cancellationToken)
    {
        Debug.Assert(runtime is not null, "Activation precedes lease acquisition.");
        Debug.Assert(address is not null, "Public entry points validate the operation address.");
        var request = new ExecutionLeaseRequest(address, _workerIds.Create(), _options.Value.LeaseDuration);
        var acquisition = await runtime.LeaseManager.AcquireAsync(request, cancellationToken).ConfigureAwait(false);
        if (acquisition is ExecutionLeaseAcquired acquired)
        {
            LogCompleted(DurableCoordinatorStage.AcquireLease, DurableCoordinatorOutcome.Completed);
            return acquired.Lease;
        }

        LogLeaseLost(DurableCoordinatorStage.AcquireLease);
        throw Fail(
            scope,
            DurableCoordinatorStage.AcquireLease,
            DurableCoordinatorOutcome.LeaseLost,
            started,
            "Another worker owns the execution lease for this durable operation.");
    }

    private async ValueTask<DurableRecorded> WriteAsync<TPayload>(
        AgentKitActivityScope scope,
        DurableCoordinatorStage stage,
        IDurabilityRuntimeLease runtime,
        DurableOperationAddress address,
        TPayload payload,
        InputFingerprint fingerprint,
        SecurityEffect effect,
        FencingToken fence,
        long started,
        Func<AuthorizedDurableRequest<TPayload>, CancellationToken, ValueTask<DurableRecordResult>> write,
        CancellationToken cancellationToken)
        where TPayload : class
    {
        Debug.Assert(payload is not null, "Every durable write carries a payload.");
        Debug.Assert(write is not null, "Callers supply the journal method performing the write.");
        var journalKey = runtime.Context.JournalKey;
        var grant = await AuthorizeAsync(
            scope,
            stage,
            runtime,
            address,
            runtime.Journal.SecurityAudience,
            fingerprint,
            SecurityOperationKind.StateMutation,
            effect,
            started,
            cancellationToken).ConfigureAwait(false);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), fence);
        var request = new AuthorizedDurableRequest<TPayload>(payload, journalKey, grant, intent);
        var result = await write(request, cancellationToken).ConfigureAwait(false);
        switch (result)
        {
            case DurableRecorded recorded:
                LogCompleted(stage, DurableCoordinatorOutcome.Completed);
                return recorded;
            case DurableRecordFenced:
                LogLeaseLost(stage);
                throw Fail(
                    scope,
                    stage,
                    DurableCoordinatorOutcome.LeaseLost,
                    started,
                    "A newer ownership generation rejected this durable write.");
            case DurableRecordFailed failure:
                throw Fail(scope, stage, DurableCoordinatorOutcome.Failed, started, failure.SafeMessage);
            default:
                throw Fail(
                    scope,
                    stage,
                    DurableCoordinatorOutcome.Failed,
                    started,
                    "The journal returned an unrecognized durable write result.");
        }
    }

    private async ValueTask<SecurityGrant> AuthorizeAsync(
        AgentKitActivityScope scope,
        DurableCoordinatorStage stage,
        IDurabilityRuntimeLease runtime,
        DurableOperationAddress address,
        ComponentId audience,
        InputFingerprint fingerprint,
        SecurityOperationKind kind,
        SecurityEffect effect,
        long started,
        CancellationToken cancellationToken)
    {
        var (grant, outcome, reason) = await TryAuthorizeAsync(
            runtime, address, audience, fingerprint, kind, effect, cancellationToken).ConfigureAwait(false);
        return grant ?? throw Fail(scope, stage, outcome, started, reason!);
    }

    /// <summary>Authorizes one protected durable write and reports refusal as a value instead of an exception.</summary>
    /// <param name="runtime">The activated runtime naming the captured authority and journal.</param>
    /// <param name="address">The operation address the write is bound to.</param>
    /// <param name="audience">The enforcing component identity the grant must name.</param>
    /// <param name="fingerprint">The canonical digest of the exact record being written.</param>
    /// <param name="kind">The protected operation kind for this write.</param>
    /// <param name="effect">The material effect the write performs.</param>
    /// <param name="cancellationToken">Cancels the authorization attempt.</param>
    /// <returns>
    /// The issued grant with <see cref="DurableCoordinatorOutcome.Completed"/>, or a null grant with the bounded
    /// refusal outcome and its content-free reason.
    /// </returns>
    private async ValueTask<(SecurityGrant? Grant, DurableCoordinatorOutcome Outcome, string? Reason)> TryAuthorizeAsync(
        IDurabilityRuntimeLease runtime,
        DurableOperationAddress address,
        ComponentId audience,
        InputFingerprint fingerprint,
        SecurityOperationKind kind,
        SecurityEffect effect,
        CancellationToken cancellationToken)
    {
        Debug.Assert(runtime is not null, "Activation precedes every authorized durable write.");
        Debug.Assert(address is not null, "Every authorized durable write names an operation address.");
        var authorization = runtime.Context.Authorization;
        var selection = await _securityAuthorities.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (selection is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return (
                null,
                DurableCoordinatorOutcome.Unavailable,
                "The security authority named by the captured durability context is unavailable.");
        }

        var request = new SecurityRequest(
            _securityRequestIds.Create(),
            authorization.Scope,
            toolCallId: null,
            authorization.Identity,
            authorization,
            audience,
            kind,
            effect,
            [DurableJournalSecurityBinding.Resource(runtime.Context.JournalKey, address)],
            fingerprint,
            _timeProvider.GetUtcNow().Add(_options.Value.LeaseDuration));
        var decision = await selected.Authority
            .AuthorizeAsync(request, hooks: null, cancellationToken).ConfigureAwait(false);
        return decision is SecurityAllowed allowed
            ? (allowed.Grant, DurableCoordinatorOutcome.Completed, null)
            : (
                null,
                DurableCoordinatorOutcome.Denied,
                decision is SecurityDenied denied
                    ? denied.Denial.SafeMessage
                    : "The durable journal write was not authorized.");
    }

    /// <summary>Gets the current injected-clock instant used for coordinator-owned record timestamps.</summary>
    /// <value>An instant from the engine's <see cref="TimeProvider"/>, never an ambient clock.</value>
    internal DateTimeOffset Now => _timeProvider.GetUtcNow();

    /// <summary>Allocates one checkpoint identity from the injected generator.</summary>
    /// <returns>A fresh identity for a handler-requested checkpoint.</returns>
    /// <remarks>Identity allocation stays with the coordinator so a handler cannot reuse or forge a checkpoint identity.</remarks>
    internal CheckpointId NextCheckpointId() => _checkpointIds.Create();

    /// <summary>Authorizes and commits one handler-requested mid-operation record, reporting refusal as a value.</summary>
    /// <typeparam name="TPayload">The durable record shape being written.</typeparam>
    /// <param name="stage">The bounded stage reported to metrics and logs.</param>
    /// <param name="runtime">The activated runtime whose journal and authority were captured for this attempt.</param>
    /// <param name="address">The operation address the record belongs to.</param>
    /// <param name="payload">The non-null complete record to commit.</param>
    /// <param name="fingerprint">The canonical digest of <paramref name="payload"/>.</param>
    /// <param name="effect">The material effect this write performs.</param>
    /// <param name="fence">The active lease generation the write must carry.</param>
    /// <param name="write">The fenced journal method performing the write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The journal's own result, or a <see cref="DurableRecordFailed"/> describing an authorization refusal.</returns>
    /// <remarks>
    /// A handler must be able to distinguish lost ownership from a local fault, so this path never converts a
    /// refusal into an exception and never rewrites the journal's own verdict.
    /// </remarks>
    internal async ValueTask<DurableRecordResult> WriteForHandlerAsync<TPayload>(
        DurableCoordinatorStage stage,
        IDurabilityRuntimeLease runtime,
        DurableOperationAddress address,
        TPayload payload,
        InputFingerprint fingerprint,
        SecurityEffect effect,
        FencingToken fence,
        Func<AuthorizedDurableRequest<TPayload>, CancellationToken, ValueTask<DurableRecordResult>> write,
        CancellationToken cancellationToken)
        where TPayload : class
    {
        Debug.Assert(payload is not null, "Every durable write carries a payload.");
        Debug.Assert(write is not null, "Callers supply the journal method performing the write.");
        var (grant, outcome, reason) = await TryAuthorizeAsync(
            runtime,
            address,
            runtime.Journal.SecurityAudience,
            fingerprint,
            SecurityOperationKind.StateMutation,
            effect,
            cancellationToken).ConfigureAwait(false);
        if (grant is null)
        {
            LogCompleted(stage, outcome);
            return new DurableRecordFailed(reason!, committed: false);
        }

        var request = new AuthorizedDurableRequest<TPayload>(
            payload, runtime.Context.JournalKey, grant, new SecurityEnforcementIntent(_intentIds.Create(), fence));
        var result = await write(request, cancellationToken).ConfigureAwait(false);
        switch (result)
        {
            case DurableRecordFenced:
                LogLeaseLost(stage);
                break;
            case DurableRecorded:
                LogCompleted(stage, DurableCoordinatorOutcome.Completed);
                break;
            default:
                LogCompleted(stage, DurableCoordinatorOutcome.Failed);
                break;
        }

        return result;
    }

    /// <summary>Publishes one handler-caused durable transition to the sinks selected for the captured context.</summary>
    /// <param name="context">The captured durability context whose sinks receive the event.</param>
    /// <param name="executionEvent">The non-null immutable transition to publish.</param>
    /// <param name="cancellationToken">Cancels local awaiting of delivery.</param>
    /// <returns>A task that completes once the dispatcher returns.</returns>
    internal async ValueTask PublishForHandlerAsync(
        DurableExecutionContext context,
        DurableExecutionEvent executionEvent,
        CancellationToken cancellationToken) =>
        await PublishAsync(context, executionEvent, cancellationToken).ConfigureAwait(false);

    private async ValueTask<DurableOperationResult> InvokeHandlerAsync(
        IDurabilityRuntimeLease runtime,
        FencedDurableOperationJournal journal,
        RecoverableOperationDescriptor operation,
        IExecutionLease lease,
        IDurableOperationHandler handler,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken)
    {
        Debug.Assert(handler is not null, "A handler is resolved before invocation.");
        Debug.Assert(handler.OperationName == operation.Name, "Handlers are resolved by the declaration's own name.");

        // The writer is revoked in a finally block rather than left alive, because a handler that captured it into
        // background work would otherwise commit records under a lease this attempt no longer owns.
        var writer = new CoordinatorCheckpointWriter(this, runtime, journal, operation, lease);
        try
        {
            var context = new DurableInvocationContext(operation, lease, writer, hooks);
            return await handler.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writer.Revoke();
        }
    }

    private async ValueTask<RecoveryEvidence> LoadEvidenceAsync(
        AgentKitActivityScope scope,
        IDurabilityRuntimeLease runtime,
        DurableOperationAddress address,
        long started,
        CancellationToken cancellationToken)
    {
        var grant = await AuthorizeAsync(
            scope,
            DurableCoordinatorStage.LoadEvidence,
            runtime,
            address,
            runtime.Journal.SecurityAudience,
            DurableJournalSecurityBinding.Fingerprint(address),
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            started,
            cancellationToken).ConfigureAwait(false);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), requiredFence: null);
        var request = new AuthorizedDurableRequest<DurableOperationAddress>(
            address, runtime.Context.JournalKey, grant, intent);
        var result = await runtime.Journal.LoadEvidenceAsync(request, cancellationToken).ConfigureAwait(false);
        switch (result)
        {
            case RecoveryEvidenceLoaded loaded:
                DurabilityMetrics.RecordStage(DurableCoordinatorStage.LoadEvidence, DurableCoordinatorOutcome.Completed);
                return loaded.Evidence;
            case RecoveryEvidenceNotFound:
                throw Fail(
                    scope,
                    DurableCoordinatorStage.LoadEvidence,
                    DurableCoordinatorOutcome.NotPossible,
                    started,
                    "No durable record exists for the requested operation address.");
            case RecoveryEvidenceUnavailable unavailable:
                throw Fail(
                    scope,
                    DurableCoordinatorStage.LoadEvidence,
                    DurableCoordinatorOutcome.Unavailable,
                    started,
                    unavailable.SafeMessage);
            default:
                throw Fail(
                    scope,
                    DurableCoordinatorStage.LoadEvidence,
                    DurableCoordinatorOutcome.Unavailable,
                    started,
                    "The journal returned an unrecognized evidence result.");
        }
    }

    private async ValueTask DispatchAsync(
        AgentKitActivityScope scope,
        IDurabilityRuntimeLease runtime,
        FencedDurableOperationJournal journal,
        RecoverableOperationDescriptor operation,
        IExecutionLease lease,
        long started,
        CancellationToken cancellationToken)
    {
        // A backend that claims no external handoff is never asked to perform one. Asking would force it to invent an
        // external reference, and a fabricated reference would later route recovery into a reconciliation that no
        // system can answer.
        if (!runtime.Backend.Descriptor.Capabilities.SupportsExternalHandoff)
        {
            return;
        }

        using var dispatchScope = AgentKitActivityScope.Start(AgentKitActivityNames.DurableDispatch, ActivityKind.Client);
        var request = new DurableDispatchRequest(operation, runtime.Context);
        var dispatch = await runtime.Backend.DispatchAsync(request, cancellationToken).ConfigureAwait(false);
        if (dispatch is not DurableDispatched dispatched)
        {
            var reason = dispatch is DurableDispatchFailed failed
                ? failed.SafeMessage
                : "The selected durable backend refused the handoff.";
            dispatchScope.Activity.SetFailed(
                DurableCoordinatorOutcome.Failed.ToStableValue(), nameof(InvalidOperationException));
            throw Fail(scope, DurableCoordinatorStage.Dispatch, DurableCoordinatorOutcome.Failed, started, reason);
        }

        // The handoff already happened, so the reference is recorded before the local wait begins. If this process
        // dies next, recovery finds a waiting record naming the owner that may still be running the effect.
        var deferredAt = _timeProvider.GetUtcNow();
        var waiting = new DurableOperationWaiting(
            operation.Binding,
            lease.FencingToken,
            deferredAt,
            SideEffectCertainty.Unknown,
            dispatched.ExternalReference);
        _ = await WriteAsync(
            scope,
            DurableCoordinatorStage.Dispatch,
            runtime,
            operation.Address,
            waiting,
            DurableJournalSecurityBinding.Fingerprint(waiting),
            SecurityEffect.Mutate,
            lease.FencingToken,
            started,
            journal.RecordWaitingAsync,
            cancellationToken).ConfigureAwait(false);
        await PublishAsync(
            runtime.Context,
            new DurableOperationDeferred(
                operation.Binding,
                deferredAt,
                SideEffectCertainty.Unknown,
                lease.FencingToken,
                externalReference: dispatched.ExternalReference),
            cancellationToken).ConfigureAwait(false);

        LogCompleted(DurableCoordinatorStage.Dispatch, DurableCoordinatorOutcome.Completed);
        dispatchScope.Activity.SetSuccessful(DurableCoordinatorOutcome.Completed.ToStableValue());
    }

    private async ValueTask CommitTerminalAsync(
        AgentKitActivityScope scope,
        IDurabilityRuntimeLease runtime,
        FencedDurableOperationJournal journal,
        DurableOperationAddress address,
        IExecutionLease lease,
        DurableOperationResult result,
        long started,
        CancellationToken cancellationToken)
    {
        _ = await WriteAsync(
            scope,
            DurableCoordinatorStage.RecordTerminal,
            runtime,
            address,
            result,
            DurableJournalSecurityBinding.Fingerprint(result),
            SecurityEffect.Append,
            lease.FencingToken,
            started,
            journal.RecordTerminalAsync,
            cancellationToken).ConfigureAwait(false);
        await PublishAsync(
            runtime.Context,
            new DurableOperationSettled(
                result.Binding, result.CompletedAt, result.State, result.SideEffectCertainty, result.FencingToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DurableOperationResult> CommitRecordedAsync(
        AgentKitActivityScope scope,
        IDurabilityRuntimeLease runtime,
        FencedDurableOperationJournal journal,
        RecoverableOperationDescriptor operation,
        IExecutionLease lease,
        RecoveryCommitRecordedResult decision,
        DurableOperationState recordedState,
        long started,
        CancellationToken cancellationToken)
    {
        // An operation whose evidence already shows a settled state reached its terminal record in an earlier
        // attempt. Re-stamping that record under this attempt's generation would ask the journal to replace a
        // terminal result with a different one, which it refuses, so recovery reports the recorded outcome as it
        // stands and writes nothing.
        if (recordedState is DurableOperationState.Completed or DurableOperationState.Faulted)
        {
            Succeed(scope, DurableCoordinatorStage.Recover, DurableCoordinatorOutcome.Committed, started);
            return decision.Result;
        }

        // A recorded terminal result is committed exactly as it was produced, under this attempt's generation.
        // Re-recording the outcome is a projection, never a second invocation of the effect.
        var result = decision.Result with { FencingToken = lease.FencingToken };
        await CommitTerminalAsync(
            scope, runtime, journal, operation.Address, lease, result, started, cancellationToken).ConfigureAwait(false);
        Succeed(scope, DurableCoordinatorStage.Recover, DurableCoordinatorOutcome.Committed, started);
        return result;
    }

    private async ValueTask<DurableOperationResult> ReplayAsync(
        AgentKitActivityScope scope,
        IDurabilityRuntimeLease runtime,
        FencedDurableOperationJournal journal,
        RecoverableOperationDescriptor operation,
        IExecutionLease lease,
        HookDispatchContext? hooks,
        long started,
        CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(operation.Name, out var handler))
        {
            throw Fail(
                scope,
                DurableCoordinatorStage.Recover,
                DurableCoordinatorOutcome.Incompatible,
                started,
                "No durable operation handler in this build owns the recorded operation name.");
        }

        var result = await InvokeHandlerAsync(runtime, journal, operation, lease, handler, hooks, cancellationToken)
                .ConfigureAwait(false)
            ?? throw Fail(
                scope,
                DurableCoordinatorStage.Recover,
                DurableCoordinatorOutcome.Failed,
                started,
                "The durable operation handler returned no terminal result.");
        await CommitTerminalAsync(
            scope, runtime, journal, operation.Address, lease, result, started, cancellationToken).ConfigureAwait(false);
        Succeed(scope, DurableCoordinatorStage.Recover, DurableCoordinatorOutcome.Started, started);
        return result;
    }

    private async ValueTask<DurableOperationResult> ReconcileAsync(
        AgentKitActivityScope scope,
        IDurabilityRuntimeLease runtime,
        FencedDurableOperationJournal journal,
        RecoverableOperationDescriptor operation,
        IExecutionLease lease,
        RecoveryEvidence evidence,
        long started,
        CancellationToken cancellationToken)
    {
        // The reconciliation request carries the evidence, which already names the external reference the policy
        // matched on, so the decision itself adds nothing the backend needs.
        using var reconcileScope = AgentKitActivityScope.Start(AgentKitActivityNames.DurableReconcile, ActivityKind.Client);
        var outcome = await runtime.Backend
            .ReconcileAsync(new DurableReconciliationRequest(operation, evidence), cancellationToken)
            .ConfigureAwait(false);
        if (outcome is not DurableReconciled reconciled)
        {
            var reason = outcome is DurableReconciliationFailed failed
                ? failed.SafeMessage
                : "The selected durable backend could not establish the true external outcome.";
            reconcileScope.Activity.SetFailed(
                DurableCoordinatorOutcome.Unavailable.ToStableValue(), nameof(InvalidOperationException));
            LogOperatorRequired(DurableCoordinatorOutcome.Unavailable);
            throw Fail(scope, DurableCoordinatorStage.Reconcile, DurableCoordinatorOutcome.Unavailable, started, reason);
        }

        reconcileScope.Activity.SetSuccessful(DurableCoordinatorOutcome.Reconciled.ToStableValue());

        // Reconciliation establishes what the external owner actually did, not what it returned. The terminal record
        // therefore carries an empty payload under the declared input schema rather than echoing the input as though
        // it were an output.
        var result = new DurableOperationResult(
            operation.Binding,
            DurableOperationState.Completed,
            reconciled.SideEffectCertainty,
            new OperationPayload(operation.Input.SchemaVersion, []),
            lease.FencingToken,
            _timeProvider.GetUtcNow());
        await CommitTerminalAsync(
            scope, runtime, journal, operation.Address, lease, result, started, cancellationToken).ConfigureAwait(false);
        Succeed(scope, DurableCoordinatorStage.Recover, DurableCoordinatorOutcome.Reconciled, started);
        return result;
    }

    private async ValueTask PublishAsync(
        DurableExecutionContext context,
        DurableExecutionEvent executionEvent,
        CancellationToken cancellationToken) =>
        await _events.PublishAsync(context, executionEvent, cancellationToken).ConfigureAwait(false);

    private InvalidOperationException Fail(
        AgentKitActivityScope scope,
        DurableCoordinatorStage stage,
        DurableCoordinatorOutcome outcome,
        long started,
        string safeReason)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(safeReason), "Every refusal carries content-free text.");
        Observe(() =>
        {
            DurabilityMetrics.RecordStage(stage, outcome, Elapsed(started));
            var stageValue = stage.ToStableValue();
            var outcomeValue = outcome.ToStableValue();
            if (outcome is DurableCoordinatorOutcome.Unavailable or DurableCoordinatorOutcome.LeaseLost)
            {
                DurabilityLog.StageUnavailable(_logger, stageValue, outcomeValue);
            }
            else
            {
                DurabilityLog.StageFailed(_logger, stageValue, nameof(InvalidOperationException));
            }
        });
        scope.Activity.SetFailed(outcome.ToStableValue(), nameof(InvalidOperationException));
        return new InvalidOperationException(safeReason);
    }

    private InvalidOperationException FailOperator(AgentKitActivityScope scope, long started)
    {
        LogOperatorRequired(DurableCoordinatorOutcome.OperatorRequired);
        return Fail(
            scope,
            DurableCoordinatorStage.Decide,
            DurableCoordinatorOutcome.OperatorRequired,
            started,
            "The recorded evidence requires operator action or reconciliation before any retry.");
    }

    private void Succeed(
        AgentKitActivityScope scope,
        DurableCoordinatorStage stage,
        DurableCoordinatorOutcome outcome,
        long started)
    {
        Observe(() =>
        {
            DurabilityMetrics.RecordStage(stage, outcome, Elapsed(started));
            var stageValue = stage.ToStableValue();
            var outcomeValue = outcome.ToStableValue();
            DurabilityLog.StageCompleted(_logger, stageValue, outcomeValue);
        });
        scope.Activity.SetSuccessful(outcome.ToStableValue());
    }

    private void Cancel(AgentKitActivityScope scope, DurableCoordinatorStage stage, long started)
    {
        Observe(() =>
        {
            DurabilityMetrics.RecordStage(stage, DurableCoordinatorOutcome.Cancelled, Elapsed(started));
            var stageValue = stage.ToStableValue();
            DurabilityLog.StageCancelled(_logger, stageValue);
        });
        scope.Activity.SetFailed(
            DurableCoordinatorOutcome.Cancelled.ToStableValue(), nameof(OperationCanceledException));
    }

    private void LogCompleted(DurableCoordinatorStage stage, DurableCoordinatorOutcome outcome) =>
        Observe(() =>
        {
            DurabilityMetrics.RecordStage(stage, outcome);
            var stageValue = stage.ToStableValue();
            var outcomeValue = outcome.ToStableValue();
            DurabilityLog.StageCompleted(_logger, stageValue, outcomeValue);
        });

    private void LogLeaseLost(DurableCoordinatorStage stage) =>
        Observe(() => DurabilityLog.LeaseLost(_logger, stage.ToStableValue()));

    private void LogOperatorRequired(DurableCoordinatorOutcome outcome) =>
        Observe(() => DurabilityLog.OperatorActionRequired(_logger, outcome.ToStableValue()));

    /// <summary>Runs one observational emission so a failing logging or metrics provider cannot change behavior.</summary>
    /// <param name="observation">The non-null emission to attempt.</param>
    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "An observational delegate is required.");
        try
        {
            observation();
        }
        catch
        {
            // Instrumentation is observational. A failing provider never mutates a durable semantic outcome.
        }
    }

    private TimeSpan? Elapsed(long started)
    {
        try
        {
            var elapsed = _timeProvider.GetElapsedTime(started);
            return elapsed < TimeSpan.Zero ? null : elapsed;
        }
        catch (Exception)
        {
            // An unavailable diagnostic clock omits the measurement instead of reporting a false zero.
            return null;
        }
    }
}
