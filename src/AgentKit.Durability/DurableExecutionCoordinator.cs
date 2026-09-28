// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

using Microsoft.Extensions.Options;

/// <summary>Drives one durable operation from acceptance to a terminal durable record.</summary>
/// <remarks>
/// <para>
/// Execution follows a fixed order so recovery can always tell what may safely happen next: activate the runtime named
/// by the captured context, acquire an execution lease, commit the acceptance record <em>before</em> any effect, hand
/// off to the selected backend, invoke the owning handler, then commit the terminal record. Each durable write is a
/// protected operation authorized through the authority named by the captured
/// <see cref="DurableExecutionContext.Authorization"/> and carries the lease's current fencing generation.
/// </para>
/// <para>
/// Recovery never reinvokes an effect on its own initiative. It loads evidence, asks the profile-selected
/// <see cref="IRecoveryPolicy"/>, and performs only the action that policy permits. A recorded terminal result is
/// committed without invocation; an unknown non-idempotent outcome becomes operator action.
/// </para>
/// <para>
/// The coordinator is a thread-safe singleton. Renewal runs on the injected <see cref="TimeProvider"/> for the
/// duration of one attempt, and cancellation stops local awaiting and renewal only: it never asserts that a
/// handed-off backend operation stopped.
/// </para>
/// </remarks>
internal sealed partial class DurableExecutionCoordinator: IDurableExecutionCoordinator
{
    private readonly IDurabilityRuntimeSelector _runtimeSelector;
    private readonly ISecurityAuthoritySelector _securityAuthorities;
    private readonly IDurableExecutionEventDispatcher _events;
    private readonly IIdentifierGenerator<WorkerId> _workerIds;
    private readonly IIdentifierGenerator<CheckpointId> _checkpointIds;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly IOptions<AgentDurabilityOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly Dictionary<DurableOperationName, IDurableOperationHandler> _handlers;

    /// <summary>Initializes the coordinator over its engine-wide collaborators.</summary>
    /// <param name="runtimeSelector">Activates the components named by a captured context.</param>
    /// <param name="securityAuthorities">Selects the authority that authorizes each protected durable write.</param>
    /// <param name="events">Publishes immutable durable transitions to registered sinks.</param>
    /// <param name="handlers">The additive handlers that own each recoverable operation name.</param>
    /// <param name="workerIds">Allocates a fresh worker identity per attempt so takeover stays observable.</param>
    /// <param name="checkpointIds">Allocates identities for the mid-operation checkpoints handlers request.</param>
    /// <param name="securityRequestIds">Allocates authorization request identities.</param>
    /// <param name="intentIds">Allocates enforcement-intent identities for exact grant consumption.</param>
    /// <param name="options">The engine-wide lease and recovery ceilings.</param>
    /// <param name="timeProvider">The injected clock used for renewal scheduling and record instants.</param>
    /// <param name="logger">The optional logger for content-free coordinator diagnostics.</param>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    /// <exception cref="ArgumentException">Two handlers claim the same operation name.</exception>
    public DurableExecutionCoordinator(
        IDurabilityRuntimeSelector runtimeSelector,
        ISecurityAuthoritySelector securityAuthorities,
        IDurableExecutionEventDispatcher events,
        IEnumerable<IDurableOperationHandler> handlers,
        IIdentifierGenerator<WorkerId> workerIds,
        IIdentifierGenerator<CheckpointId> checkpointIds,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        IOptions<AgentDurabilityOptions> options,
        TimeProvider timeProvider,
        ILogger<DurableExecutionCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeSelector);
        ArgumentNullException.ThrowIfNull(securityAuthorities);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(workerIds);
        ArgumentNullException.ThrowIfNull(checkpointIds);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _handlers = [];
        foreach (var handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler, nameof(handlers));
            if (!_handlers.TryAdd(handler.OperationName, handler))
            {
                throw new ArgumentException(
                    "Each recoverable operation name must be owned by exactly one durable operation handler.",
                    nameof(handlers));
            }
        }

        _runtimeSelector = runtimeSelector;
        _securityAuthorities = securityAuthorities;
        _events = events;
        _workerIds = workerIds;
        _checkpointIds = checkpointIds;
        _securityRequestIds = securityRequestIds;
        _intentIds = intentIds;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger ?? (ILogger) NullLogger.Instance;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// No handler owns the operation name, the captured runtime could not be activated, the execution lease is held by
    /// another worker, or a required durable write was refused. None of these leaves a partial durable record.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<DurableOperationResult> ExecuteAsync(
        RecoverableOperationDescriptor operation,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = AgentKitActivityScope.Start(AgentKitActivityNames.DurableExecute, ActivityKind.Internal);
        var started = _timeProvider.GetTimestamp();
        try
        {
            if (!_handlers.TryGetValue(operation.Name, out var handler))
            {
                throw Fail(
                    scope,
                    DurableCoordinatorStage.Execute,
                    DurableCoordinatorOutcome.Unavailable,
                    started,
                    "No durable operation handler is registered for the requested operation name.");
            }

            await using var runtime = await ActivateAsync(scope, operation.ExecutionContext, started, cancellationToken)
                .ConfigureAwait(false);
            await using var lease = await AcquireAsync(scope, runtime, operation.Address, started, cancellationToken)
                .ConfigureAwait(false);
            var journal = new FencedDurableOperationJournal(runtime.Journal, lease);
            using var renewal = new ExecutionLeaseRenewal(
                lease,
                _timeProvider,
                _options.Value.LeaseRenewalInterval,
                _logger);

            var acceptedAt = _timeProvider.GetUtcNow();
            var start = new DurableOperationStart(operation, operation.Input, lease.FencingToken, acceptedAt);
            _ = await WriteAsync(
                scope,
                DurableCoordinatorStage.RecordStart,
                runtime,
                operation.Address,
                start,
                DurableJournalSecurityBinding.Fingerprint(start),
                SecurityEffect.Create,
                lease.FencingToken,
                started,
                journal.RecordStartAsync,
                cancellationToken).ConfigureAwait(false);
            await PublishAsync(
                runtime.Context,
                new DurableOperationAccepted(
                    operation.Binding, acceptedAt, operation.Name, operation.Version, lease.FencingToken),
                cancellationToken).ConfigureAwait(false);

            await DispatchAsync(scope, runtime, journal, operation, lease, started, cancellationToken)
                .ConfigureAwait(false);
            var result = await InvokeHandlerAsync(runtime, journal, operation, lease, handler, hooks, cancellationToken)
                    .ConfigureAwait(false)
                ?? throw Fail(
                    scope,
                    DurableCoordinatorStage.Execute,
                    DurableCoordinatorOutcome.Failed,
                    started,
                    "The durable operation handler returned no terminal result.");

            await CommitTerminalAsync(scope, runtime, journal, operation.Address, lease, result, started, cancellationToken)
                .ConfigureAwait(false);
            Succeed(scope, DurableCoordinatorStage.Execute, DurableCoordinatorOutcome.Completed, started);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Cancel(scope, DurableCoordinatorStage.Execute, started);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The captured runtime could not be activated, evidence was unavailable or absent, the retained declaration is
    /// missing, the lease is held by another worker, policy permits no automatic action, or a required durable write
    /// was refused.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<DurableOperationResult> RecoverAsync(
        DurableOperationAddress address,
        DurableExecutionContext context,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = AgentKitActivityScope.Start(AgentKitActivityNames.DurableRecover, ActivityKind.Internal);
        var started = _timeProvider.GetTimestamp();
        try
        {
            await using var runtime = await ActivateAsync(scope, context, started, cancellationToken).ConfigureAwait(false);
            await using var lease = await AcquireAsync(scope, runtime, address, started, cancellationToken).ConfigureAwait(false);
            var journal = new FencedDurableOperationJournal(runtime.Journal, lease);
            using var renewal = new ExecutionLeaseRenewal(
                lease,
                _timeProvider,
                _options.Value.LeaseRenewalInterval,
                _logger);

            var evidence = await LoadEvidenceAsync(scope, runtime, address, started, cancellationToken).ConfigureAwait(false);
            if (evidence.Descriptor is not { } operation)
            {
                throw Fail(
                    scope,
                    DurableCoordinatorStage.LoadEvidence,
                    DurableCoordinatorOutcome.Incompatible,
                    started,
                    "Recovery evidence retains no accepted declaration, so the operation cannot be classified.");
            }

            var decision = await runtime.RecoveryPolicy
                .DecideAsync(operation, evidence, cancellationToken).ConfigureAwait(false);
            await PublishAsync(
                runtime.Context,
                new DurableOperationRecoveryPlanned(
                    operation.Binding,
                    _timeProvider.GetUtcNow(),
                    ToAction(decision),
                    evidence.State,
                    evidence.SideEffectCertainty),
                cancellationToken).ConfigureAwait(false);

            return decision switch
            {
                RecoveryCommitRecordedResult commit =>
                    await CommitRecordedAsync(scope, runtime, journal, operation, lease, commit, started, cancellationToken)
                        .ConfigureAwait(false),
                RecoveryStartOperation or RecoveryRetryOperation =>
                    await ReplayAsync(scope, runtime, journal, operation, lease, hooks, started, cancellationToken)
                        .ConfigureAwait(false),
                RecoveryReconcileOperation =>
                    await ReconcileAsync(
                        scope, runtime, journal, operation, lease, evidence, started, cancellationToken)
                        .ConfigureAwait(false),
                RecoveryRequiresOperator => throw FailOperator(scope, started),
                _ => throw Fail(
                    scope,
                    DurableCoordinatorStage.Decide,
                    DurableCoordinatorOutcome.NotPossible,
                    started,
                    "The recovery policy permits no automatic action for the loaded evidence."),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Cancel(scope, DurableCoordinatorStage.Recover, started);
            throw;
        }
    }

    private static DurableRecoveryAction ToAction(RecoveryDecision decision) => decision switch
    {
        RecoveryStartOperation => DurableRecoveryAction.Start,
        RecoveryRetryOperation => DurableRecoveryAction.Retry,
        RecoveryReconcileOperation => DurableRecoveryAction.Reconcile,
        RecoveryCommitRecordedResult => DurableRecoveryAction.CommitRecordedResult,
        RecoveryRequiresOperator => DurableRecoveryAction.RequireOperator,
        _ => DurableRecoveryAction.NotPossible,
    };
}
