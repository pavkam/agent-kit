// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;

using Microsoft.Extensions.Options;

/// <summary>
/// Runs the spec-shaped tool pipeline: resolve, validate, plan, authorize, record the accepted call, schedule, invoke
/// with policy-planned retries, normalize, and record one terminal <see cref="ToolCallResult"/> per call.
/// </summary>
/// <remarks>
/// <para>
/// Stages run in this order for every call. Resolution, argument validation, and execution-policy selection and planning
/// precede authorization. An authorized call is committed through <see cref="IToolCallRecorder"/> <em>before</em> its
/// invoker starts: if the accepted record cannot be made durable the call fails closed with a typed
/// <see cref="ToolTerminalStatus.Unsupported"/> result, certainty
/// <see cref="SideEffectCertainty.DefinitelyNotPerformed"/>, and no effect. The scheduler then invokes accepted entries
/// under the planned retry policy, and every call, including each pre-invocation rejection, receives exactly one
/// terminal result that is recorded after any result hook and before it is returned.
/// </para>
/// <para>
/// A terminal record that cannot be committed never changes the outcome: the effect, if any, has already happened, so the
/// result is returned unchanged, the failure is logged, and <see cref="ToolCallTerminalEvent.Recorded"/> reports it.
/// Terminal records and terminal events are committed independently of the caller's cancellation so a settled call is
/// never left without evidence. Events reach <see cref="IToolEventSink"/> implementations through
/// <see cref="ToolEventDispatcher"/>, which isolates every sink failure. This executor does not invoke
/// <see cref="IToolResultProjector"/>; the loop projects the recorded result.
/// </para>
/// <para>
/// Instances are immutable after construction and safe for concurrent use. The recorder is the one the host selected for
/// this executor; the executor never resolves a session coordinator itself.
/// </para>
/// </remarks>
public sealed class DefaultToolExecutor: IToolExecutor
{
    private readonly IToolResolver _resolver;
    private readonly IToolArgumentValidator _argumentValidator;
    private readonly IToolExecutionPolicySelector _policySelector;
    private readonly ISecurityAuthoritySelector _securityAuthorities;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds;
    private readonly IToolCallRecorder _recorder;
    private readonly IToolScheduler _scheduler;
    private readonly ToolEventDispatcher _events;
    private readonly ToolSchemaLimits _argumentValidationLimits;
    private readonly ToolRuntimeOptions _runtimeOptions;
    private readonly TimeProvider _timeProvider;
    private readonly IHookDispatcher? _hookDispatcher;
    private readonly IApprovalWaitRecorder? _approvalWaits;
    private readonly IToolResultSpill? _resultSpill;
    private readonly ILogger<DefaultToolExecutor> _logger;

    /// <summary>Initializes the first-party spec-shaped tool executor.</summary>
    /// <param name="resolver">The catalog resolver that binds aliases and acquires invoker leases.</param>
    /// <param name="argumentValidator">The argument validator applied after resolution.</param>
    /// <param name="policySelector">The selector that supplies the execution policy for each call's exact captured reference.</param>
    /// <param name="securityAuthorities">The selector that activates the security authority for each call.</param>
    /// <param name="securityRequestIds">The identifier generator for invocation authorization requests.</param>
    /// <param name="recorder">The recorder that commits each accepted call before invocation and each terminal outcome after it.</param>
    /// <param name="scheduler">The scheduler that invokes accepted entries under barrier-segment policy.</param>
    /// <param name="events">The dispatcher that delivers accepted and terminal events to registered sinks.</param>
    /// <param name="argumentValidationLimits">The bounds applied to argument validation for each call.</param>
    /// <param name="runtimeOptions">The configured tool runtime limits and scheduling defaults.</param>
    /// <param name="timeProvider">The replaceable clock used for timestamps.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <param name="hookDispatcher">The optional hook dispatcher for tool lifecycle points.</param>
    /// <param name="approvalWaits">
    /// The optional recorder that journals a call whose authorization deferred to a pending approval. When omitted, a
    /// deferral is treated as an unauthorized call and leaves no durable wait.
    /// </param>
    /// <param name="resultSpill">
    /// The optional externalization of oversized results through an artifact coordinator. When omitted, an oversized result is
    /// truncated to its canonical byte bound.
    /// </param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultToolExecutor(
        IToolResolver resolver,
        IToolArgumentValidator argumentValidator,
        IToolExecutionPolicySelector policySelector,
        ISecurityAuthoritySelector securityAuthorities,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IToolCallRecorder recorder,
        IToolScheduler scheduler,
        ToolEventDispatcher events,
        ToolSchemaLimits argumentValidationLimits,
        IOptions<ToolRuntimeOptions> runtimeOptions,
        TimeProvider timeProvider,
        ILogger<DefaultToolExecutor> logger,
        IHookDispatcher? hookDispatcher = null,
        IApprovalWaitRecorder? approvalWaits = null,
        IToolResultSpill? resultSpill = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(argumentValidator);
        ArgumentNullException.ThrowIfNull(policySelector);
        ArgumentNullException.ThrowIfNull(securityAuthorities);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(argumentValidationLimits);
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _resolver = resolver;
        _argumentValidator = argumentValidator;
        _policySelector = policySelector;
        _securityAuthorities = securityAuthorities;
        _securityRequestIds = securityRequestIds;
        _recorder = recorder;
        _scheduler = scheduler;
        _events = events;
        _argumentValidationLimits = argumentValidationLimits;
        _runtimeOptions = runtimeOptions.Value;
        _timeProvider = timeProvider;
        _hookDispatcher = hookDispatcher;
        _approvalWaits = approvalWaits;
        _resultSpill = resultSpill;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ToolBatchResult> ExecuteAsync(
        IToolCatalogCapture capture,
        ImmutableArray<ToolCallRequest> calls,
        ToolExecutionCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentException.ThrowIfDefault(calls);

        if (calls.Length == 0)
        {
            return new ToolBatchResult([]);
        }

        var results = new ToolCallResult?[calls.Length];
        var staged = new List<Staged>(calls.Length);
        var admitted = new List<Admitted>(calls.Length);
        try
        {
            await StageAsync(capture, calls, capability, results, staged, cancellationToken).ConfigureAwait(false);
            var prepared = await PlanAsync(capability, results, staged, cancellationToken).ConfigureAwait(false);
            await AdmitAsync(capability, results, prepared, admitted, cancellationToken).ConfigureAwait(false);
            if (admitted.Count > 0)
            {
                await ScheduleAsync(calls[0], results, admitted, capability.Budget, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            await AbortAsync(capability, staged, admitted).ConfigureAwait(false);
            throw;
        }

        var builder = ImmutableArray.CreateBuilder<ToolCallResult>(calls.Length);
        for (var index = 0; index < results.Length; index++)
        {
            if (results[index] is not { } result)
            {
                throw new InvalidOperationException("Every tool call must produce exactly one terminal result.");
            }

            var hooked = await ToolExecutionHookDispatcher.DispatchToolResultAsync(
                _hookDispatcher,
                capability.Hooks,
                result.AgentId,
                result.SessionId,
                result.RunId,
                result,
                cancellationToken).ConfigureAwait(false);
            await SettleAsync(hooked, capability).ConfigureAwait(false);
            builder.Add(hooked);
        }

        return new ToolBatchResult(builder.ToImmutable());
    }

    private async Task StageAsync(
        IToolCatalogCapture capture,
        ImmutableArray<ToolCallRequest> calls,
        ToolExecutionCapability capability,
        ToolCallResult?[] results,
        List<Staged> staged,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < calls.Length; index++)
        {
            var request = calls[index];
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            if (capability.Hooks is { } hooks && _hookDispatcher is not null)
            {
                var callPart = ToolExecutionHookDispatcher.CreateCallPart(capture.Snapshot, request);
                var before = await ToolExecutionHookDispatcher.DispatchBeforeToolInvocationAsync(
                    _hookDispatcher,
                    hooks,
                    request.AgentId,
                    request.SessionId,
                    callPart,
                    cancellationToken).ConfigureAwait(false);
                if (before.Veto is { } veto)
                {
                    results[index] = ToolCallResultComposer.PreInvocation(
                        request,
                        ToolTerminalStatus.Unsupported,
                        $"The call was vetoed before invocation: {veto.SafeReason}",
                        toolId: null,
                        toolVersion: null,
                        effects: null,
                        ToolRuntimeNormalizationDefaults.RejectionSnapshot,
                        _timeProvider.GetUtcNow());
                    continue;
                }

                request = ToolExecutionHookDispatcher.WithRawArguments(
                    request,
                    ToolExecutionHookDispatcher.ToRawArguments(before.Arguments));
            }

            var resolution = await _resolver.ResolveAsync(capture, request, cancellationToken).ConfigureAwait(false);
            if (resolution is ToolCallUnresolved unresolved)
            {
                results[index] = ToolCallResultComposer.PreInvocation(
                    request,
                    unresolved.Status,
                    unresolved.SafeReason,
                    toolId: null,
                    toolVersion: null,
                    effects: null,
                    ToolRuntimeNormalizationDefaults.RejectionSnapshot,
                    _timeProvider.GetUtcNow());
                continue;
            }

            var resolved = (ToolCallResolved) resolution;
            var lease = resolved.Lease;
            var pending = new Staged(index, request, lease);
            staged.Add(pending);
            var validation = await _argumentValidator
                .ValidateAsync(resolved.Call, _argumentValidationLimits, cancellationToken)
                .ConfigureAwait(false);
            if (validation is ToolCallValidationFailed failed)
            {
                results[index] = ToolCallResultComposer.FromValidationFailure(failed, _timeProvider.GetUtcNow());
                await pending.ReleaseAsync().ConfigureAwait(false);
                continue;
            }

            pending.Validated = ((ToolCallValidated) validation).Call;
        }
    }

    private async Task<List<Prepared>> PlanAsync(
        ToolExecutionCapability capability,
        ToolCallResult?[] results,
        List<Staged> staged,
        CancellationToken cancellationToken)
    {
        var prepared = new List<Prepared>(staged.Count);
        var groups = new Dictionary<ToolExecutionPolicyReference, List<Staged>>();
        var order = new List<ToolExecutionPolicyReference>();
        foreach (var pending in staged)
        {
            if (pending.Validated is not { } validated || pending.Lease is null)
            {
                continue;
            }

            if (!capability.ExecutionPolicies.Any(binding => binding.Reference == validated.ExecutionPolicy))
            {
                await RejectStagedAsync(results, pending, "The tool's execution policy is not bound to this run.").ConfigureAwait(false);
                continue;
            }

            if (!groups.TryGetValue(validated.ExecutionPolicy, out var members))
            {
                members = [];
                groups[validated.ExecutionPolicy] = members;
                order.Add(validated.ExecutionPolicy);
            }

            members.Add(pending);
        }

        foreach (var reference in order)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var members = groups[reference];
            var selection = await _policySelector.SelectAsync(reference, capability, cancellationToken).ConfigureAwait(false);
            if (selection is not ToolExecutionPolicySelected selected)
            {
                foreach (var member in members)
                {
                    await RejectStagedAsync(results, member, "No execution policy is available for the tool.").ConfigureAwait(false);
                }

                continue;
            }

            var validatedCalls = members.Select(static member => member.Validated!).ToImmutableArray();
            var anchor = validatedCalls[0];
            var context = new ToolExecutionPolicyContext(
                anchor.AgentId, anchor.SessionId, anchor.RunId, anchor.CatalogVersion, _timeProvider.GetUtcNow());
            ToolExecutionPlanResult plan;
            try
            {
                plan = await selected.Policy.PlanAsync(validatedCalls, context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                await RefuseGroupAsync(results, members, reference, "faulted").ConfigureAwait(false);
                continue;
            }

            if (plan is not ToolExecutionPlanned planned)
            {
                await RefuseGroupAsync(results, members, reference, "rejected").ConfigureAwait(false);
                continue;
            }

            if (!Corresponds(planned, validatedCalls))
            {
                await RefuseGroupAsync(results, members, reference, "mismatched").ConfigureAwait(false);
                continue;
            }

            for (var offset = 0; offset < members.Count; offset++)
            {
                if (planned.Calls[offset].ExecutionPlan.Scheduling.SchedulingMode is ToolSchedulingMode.Unspecified
                    && _runtimeOptions.UnknownSchedulingMode is UnknownSchedulingMode.Reject)
                {
                    await RejectStagedAsync(
                        results,
                        members[offset],
                        "The tool call was rejected because its scheduling compatibility is unspecified and host policy refuses unknown modes.",
                        ToolTerminalStatus.Denied).ConfigureAwait(false);
                    continue;
                }

                prepared.Add(new Prepared(members[offset], planned.Calls[offset]));
            }
        }

        prepared.Sort(static (left, right) => left.Staged.Index.CompareTo(right.Staged.Index));
        return prepared;
    }

    private async Task AdmitAsync(
        ToolExecutionCapability capability,
        ToolCallResult?[] results,
        List<Prepared> prepared,
        List<Admitted> admitted,
        CancellationToken cancellationToken)
    {
        foreach (var item in prepared)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = item.Staged.Request;
            var call = item.Call.Call;
            var plan = item.Call.ExecutionPlan;
            SecurityGrant? grant;
            try
            {
                grant = await AuthorizeInvocationAsync(call, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ToolLog.Failed(_logger, request.CallId, call.Tool.Id, exception.GetType().Name);
                await RejectPreparedAsync(
                    results, item, ToolTerminalStatus.InvocationFailed, "The tool could not be authorized.", grantId: null).ConfigureAwait(false);
                continue;
            }

            if (grant is null)
            {
                await RejectPreparedAsync(
                    results, item, ToolTerminalStatus.Denied, "The tool invocation was denied.", grantId: null).ConfigureAwait(false);
                continue;
            }

            var acceptedAt = _timeProvider.GetUtcNow();
            var key = call.Tool.Effects.Idempotency is IdempotencyClassification.IdempotentWithKey
                ? new IdempotencyKey($"agentkit.tool-call:{call.RunId}:{call.CallId}")
                : (IdempotencyKey?) null;
            var accepted = new AcceptedToolCall(
                call.AgentId,
                call.SessionId,
                call.RunId,
                call.TurnId,
                call.OperationId,
                call.CallId,
                call.Authorization,
                new ToolCallAcceptanceEvidence(grant.Id, call.InputFingerprint, acceptedAt),
                call.ProviderAlias,
                call.Tool.Id,
                call.ToolVersion,
                call.Tool.Effects,
                key,
                new ToolCallAdmissionEvidence(
                    request.CatalogVersion,
                    request.SourceOrdinal,
                    ToolInvocationSecurityBinding.RawAdmissionFingerprint(request.RawArguments)),
                plan.Normalization,
                plan.Normalization.ProjectionPolicy,
                call.RequestedAt);

            var recorded = await RecordAcceptedAsync(accepted, capability, cancellationToken).ConfigureAwait(false);
            if (!recorded)
            {
                await RejectPreparedAsync(
                    results,
                    item,
                    ToolTerminalStatus.Unsupported,
                    "The accepted-call record could not be committed, so the tool was not invoked.",
                    grant.Id).ConfigureAwait(false);
                continue;
            }

            var started = _timeProvider.GetUtcNow();
            var context = new ToolInvocationContext(
                call.AgentId,
                call.SessionId,
                call.RunId,
                call.TurnId,
                call.OperationId,
                call.CallId,
                call.Tool,
                call.ToolVersion,
                call.Arguments,
                grant,
                attempt: 1,
                call.RequestedAt,
                started,
                started + plan.InvocationTimeout,
                NoopToolProgressReporter.Instance,
                capability.Session.Profile,
                key);
            var entry = new ToolBatchEntry(context, item.Staged.Lease!, item.Call, accepted);
            admitted.Add(new Admitted(item.Staged.Index, entry));

            await PublishAsync(
                new ToolCallAcceptedEvent(
                    accepted.AgentId,
                    accepted.SessionId,
                    accepted.RunId,
                    accepted.TurnId,
                    accepted.OperationId,
                    accepted.CallId,
                    _timeProvider.GetUtcNow(),
                    accepted.ToolId,
                    accepted.ToolVersion,
                    call.ExecutionPolicy),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ScheduleAsync(
        ToolCallRequest anchor,
        ToolCallResult?[] results,
        List<Admitted> admitted,
        BudgetExecutionCapability? budget,
        CancellationToken cancellationToken)
    {
        var entries = admitted.Select(static item => item.Entry).ToImmutableArray();
        var resultIndexByCallId = new Dictionary<ToolCallId, int>(admitted.Count);
        foreach (var item in admitted)
        {
            resultIndexByCallId[item.Entry.Invocation.CallId] = item.Index;
        }

        var batch = new ToolBatch(
            anchor.AgentId,
            anchor.SessionId,
            anchor.RunId,
            entries,
            _runtimeOptions.BatchFailureMode,
            _runtimeOptions.UnknownSchedulingMode,
            BatchDeadline(entries),
            budget,
            _resultSpill);
        foreach (var item in admitted)
        {
            item.HandedOff = true;
        }

        var scheduled = await _scheduler.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        foreach (var result in scheduled.Results)
        {
            if (resultIndexByCallId.TryGetValue(result.CallId, out var resultIndex))
            {
                results[resultIndex] = result;
            }
        }
    }

    private DateTimeOffset BatchDeadline(ImmutableArray<ToolBatchEntry> entries)
    {
        var timeout = TimeSpan.Zero;
        var attempts = 1;
        foreach (var entry in entries)
        {
            var plan = entry.Prepared.ExecutionPlan;
            timeout = timeout > plan.InvocationTimeout ? timeout : plan.InvocationTimeout;
            attempts = Math.Max(attempts, plan.Retry.MaximumAttempts);
        }

        var now = _timeProvider.GetUtcNow();
        var span = timeout.TotalSeconds * attempts;
        return span >= (DateTimeOffset.MaxValue - now).TotalSeconds ? DateTimeOffset.MaxValue : now.AddSeconds(span);
    }

    private async ValueTask<bool> RecordAcceptedAsync(
        AcceptedToolCall accepted,
        ToolExecutionCapability capability,
        CancellationToken cancellationToken)
    {
        try
        {
            var record = await _recorder
                .RecordAcceptedAsync(accepted, capability.Session, capability.SessionTarget, cancellationToken)
                .ConfigureAwait(false);
            return record is ToolCallRecorded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ToolLog.Failed(_logger, accepted.CallId, accepted.ToolId, exception.GetType().Name);
            return false;
        }
    }

    private async Task SettleAsync(ToolCallResult result, ToolExecutionCapability capability)
    {
        var recorded = false;
        try
        {
            var record = await _recorder
                .RecordTerminalAsync(result, capability.Session, capability.SessionTarget, CancellationToken.None)
                .ConfigureAwait(false);
            recorded = record is ToolCallRecorded;
        }
        catch (Exception exception)
        {
            ToolLog.Failed(_logger, result.CallId, result.ToolId ?? default, exception.GetType().Name);
        }

        await PublishAsync(
            new ToolCallTerminalEvent(
                result.AgentId,
                result.SessionId,
                result.RunId,
                result.TurnId,
                result.OperationId,
                result.CallId,
                _timeProvider.GetUtcNow(),
                result.ToolId,
                result.ToolVersion,
                result.Status,
                result.SideEffectCertainty,
                result.Retryable,
                accepted: result.Acceptance is not null,
                recorded),
            CancellationToken.None).ConfigureAwait(false);
    }

    private async Task PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _events.PublishAsync(toolEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Delivery is observational: the dispatcher already isolates sinks, so only an unexpected dispatcher fault lands here.
            ToolLog.Failed(_logger, toolEvent.CallId, default, exception.GetType().Name);
        }
    }

    private async Task AbortAsync(ToolExecutionCapability capability, List<Staged> staged, List<Admitted> admitted)
    {
        foreach (var pending in staged)
        {
            try
            {
                await pending.ReleaseAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                ToolLog.Failed(_logger, pending.Request.CallId, default, exception.GetType().Name);
            }
        }

        foreach (var item in admitted)
        {
            if (item.HandedOff)
            {
                continue;
            }

            var interrupted = ToolCallResultComposer.AcceptedNotStarted(
                item.Entry,
                ToolTerminalStatus.Interrupted,
                "The tool invocation was interrupted before it started.",
                _timeProvider.GetUtcNow());
            await SettleAsync(interrupted, capability).ConfigureAwait(false);
        }
    }

    private async Task RejectStagedAsync(
        ToolCallResult?[] results,
        Staged pending,
        string reason,
        ToolTerminalStatus status = ToolTerminalStatus.Unsupported)
    {
        var validated = pending.Validated!;
        results[pending.Index] = ToolCallResultComposer.PreInvocation(
            pending.Request,
            status,
            reason,
            validated.Tool.Id,
            validated.ToolVersion,
            validated.Tool.Effects,
            ToolRuntimeNormalizationDefaults.ForResolvedTool(validated.ExecutionPolicy),
            _timeProvider.GetUtcNow());
        await pending.ReleaseAsync().ConfigureAwait(false);
    }

    private async Task RefuseGroupAsync(
        ToolCallResult?[] results,
        List<Staged> members,
        ToolExecutionPolicyReference reference,
        string reason)
    {
        try
        {
            ToolLog.ExecutionPlanRefused(_logger, reference.Key, reference.Version, reason);
        }
        catch
        {
            // Instrumentation is observational only.
        }

        foreach (var member in members)
        {
            await RejectStagedAsync(results, member, "The execution policy did not plan the tool call.").ConfigureAwait(false);
        }
    }

    private async Task RejectPreparedAsync(
        ToolCallResult?[] results,
        Prepared item,
        ToolTerminalStatus status,
        string reason,
        GrantId? grantId)
    {
        var call = item.Call.Call;
        results[item.Staged.Index] = ToolCallResultComposer.PreInvocation(
            item.Staged.Request,
            status,
            reason,
            call.Tool.Id,
            call.ToolVersion,
            call.Tool.Effects,
            item.Call.ExecutionPlan.Normalization,
            _timeProvider.GetUtcNow(),
            grantId);
        await item.Staged.ReleaseAsync().ConfigureAwait(false);
    }

    private static bool Corresponds(ToolExecutionPlanned planned, ImmutableArray<ValidatedToolCall> calls)
    {
        if (planned.Calls.Length != calls.Length)
        {
            return false;
        }

        for (var index = 0; index < calls.Length; index++)
        {
            if (planned.Calls[index].Call != calls[index])
            {
                return false;
            }
        }

        return true;
    }

    private async Task<SecurityGrant?> AuthorizeInvocationAsync(ValidatedToolCall call, CancellationToken cancellationToken)
    {
        var selection = await _securityAuthorities.SelectAsync(call.Authorization, cancellationToken).ConfigureAwait(false);
        if (selection is not SecurityAuthoritySelected selected || selected.Authorization != call.Authorization)
        {
            return null;
        }

        var effect = call.Tool.Effects.Effect;
        var request = new SecurityRequest(
            id: _securityRequestIds.Create(),
            scope: call.Authorization.Scope,
            toolCallId: call.CallId,
            identity: call.Authorization.Identity,
            authorization: call.Authorization,
            audience: ToolInvocationSecurityBinding.SecurityAudience,
            kind: ToolInvocationSecurityBinding.OperationKind(effect),
            effect: ToolInvocationSecurityBinding.ToSecurityEffect(effect),
            resources: [ToolInvocationSecurityBinding.Resource(call.Tool.Id, call.ToolVersion)],
            inputFingerprint: ToolInvocationSecurityBinding.InvocationFingerprint(call.CallId, call.InputFingerprint),
            deadline: _timeProvider.GetUtcNow().Add(_runtimeOptions.InvocationTimeout));

        var decision = await selected.Authority.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        if (decision is SecurityApprovalRequired pending && _approvalWaits is not null)
        {
            // The call stays unauthorized either way; the wait is evidence that a human decision is pending, so a
            // process lost while it waits leaves a record naming the approval that would let the work resume. The
            // executor, not the authority, records it: the authority authorizes the durability journal's own writes
            // and therefore cannot depend on the coordinator that performs them.
            await _approvalWaits.RecordAsync(request, pending.Approval, cancellationToken).ConfigureAwait(false);
        }

        return decision switch
        {
            SecurityAllowed allowed => allowed.Grant,
            SecurityDenied => null,
            _ => null,
        };
    }

    private sealed class Staged(int index, ToolCallRequest request, IToolInvokerLease lease)
    {
        public int Index { get; } = index;
        public ToolCallRequest Request { get; } = request;
        public IToolInvokerLease? Lease { get; private set; } = lease;
        public ValidatedToolCall? Validated { get; set; }

        public async ValueTask ReleaseAsync()
        {
            if (Lease is { } lease)
            {
                Lease = null;
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed record Prepared(Staged Staged, PreparedToolCall Call);

    private sealed class Admitted(int index, ToolBatchEntry entry)
    {
        public int Index { get; } = index;
        public ToolBatchEntry Entry { get; } = entry;
        public bool HandedOff { get; set; }
    }
}
