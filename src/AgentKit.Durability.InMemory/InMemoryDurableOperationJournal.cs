// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Records durable operation evidence for one process under a single fencing-aware gate.</summary>
/// <remarks>
/// <para>
/// This journal enforces fencing exactly as <see cref="IDurableOperationJournal"/> requires: a write presenting an
/// ownership generation older than the current authoritative one for its address is rejected with
/// <see cref="DurableRecordFenced"/> rather than accepted. It computes <see cref="DurableOperationState"/> from which
/// method committed most recently — <see cref="DurableOperationState.Accepted"/> after acceptance,
/// <see cref="DurableOperationState.EffectPending"/> after a checkpoint, <see cref="DurableOperationState.Waiting"/>
/// after a waiting record, and the caller-supplied terminal state after a terminal record — and derives
/// <see cref="RecoveryEvidence.StartDefinitelyAbsent"/> and <see cref="SideEffectCertainty"/> from that same computed
/// state rather than trusting a separate caller claim.
/// </para>
/// <para>
/// Journal access is a protected operation. Every write consumes its single-use grant through
/// <see cref="ISecurityGrantStore"/> and completes required audit dispatch before any record is mutated, and the
/// committed <see cref="DurableRecorded"/> carries the resulting
/// <see cref="DurableJournalEnforcementReceipt"/>. Writes require the caller's current fence in their enforcement
/// intent; an authorized evidence read is unfenced. The journal fails closed: an unavailable grant store or audit
/// dispatcher denies the operation instead of writing unaudited state.
/// </para>
/// <para>
/// This adapter is explicitly ephemeral. It is useful for conformance and for a local composition that never claims
/// crash recovery, and it advertises no distributed ownership: a process restart loses every record, so absence of
/// a record here is never evidence that an effect did not occur in an earlier process.
/// </para>
/// </remarks>
public sealed partial class InMemoryDurableOperationJournal: IDurableOperationJournal
{
    private readonly Lock _gate = new();
    private readonly Dictionary<DurableOperationAddress, DurableOperationRecord> _records = [];
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly ISecurityGrantStore _grants;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemoryDurableOperationJournal> _logger;

    /// <summary>Initializes an empty journal bound to one selected key and its required security dependencies.</summary>
    /// <param name="key">The exact registration key every authorized request must target.</param>
    /// <param name="auditRecordIds">Generates the stable identity of each required audit record.</param>
    /// <param name="auditDispatcher">The required audit dispatcher that must accept every consumed access intent.</param>
    /// <param name="grants">The authoritative store that validates and consumes each exact single-use journal grant.</param>
    /// <param name="timeProvider">The non-null injected clock used for commit timestamps and elapsed measurement.</param>
    /// <param name="logger">The optional content-free structured logger.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> carries no key text, or <paramref name="auditRecordIds"/>,
    /// <paramref name="auditDispatcher"/>, <paramref name="grants"/>, or <paramref name="timeProvider"/> is null.
    /// </exception>
    /// <remarks>
    /// The security dependencies are required at construction rather than per call, so a composition cannot produce
    /// a journal that silently skips grant consumption or audit.
    /// </remarks>
    public InMemoryDurableOperationJournal(
        DurableJournalKey key,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grants,
        TimeProvider timeProvider,
        ILogger<InMemoryDurableOperationJournal>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Key = key;
        _auditRecordIds = auditRecordIds;
        _auditDispatcher = auditDispatcher;
        _grants = grants;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<InMemoryDurableOperationJournal>.Instance;
    }

    /// <summary>Gets the registration key this journal instance answers for.</summary>
    /// <value>The nonblank key an <see cref="AuthorizedDurableRequest{TRequest}"/> must name to reach this journal.</value>
    public DurableJournalKey Key { get; }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordStartAsync(
        AuthorizedDurableRequest<DurableOperationStart> start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        var operationStart = start.Request;
        var binding = operationStart.Descriptor.Binding;
        return WriteAsync(
            DurableJournalWriteOperation.RecordStart,
            start,
            binding.Address,
            binding.ExecutionContext.Authorization,
            operationStart.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(operationStart),
            SecurityEffect.Create,
            receipt => CommitStart(operationStart, binding, receipt),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordCheckpointAsync(
        AuthorizedDurableRequest<DurableCheckpoint> checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var snapshot = checkpoint.Request;
        return WriteAsync(
            DurableJournalWriteOperation.RecordCheckpoint,
            checkpoint,
            snapshot.Address,
            snapshot.ExecutionContext.Authorization,
            snapshot.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(snapshot),
            SecurityEffect.Append,
            receipt => CommitCheckpoint(snapshot, receipt),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordTerminalAsync(
        AuthorizedDurableRequest<DurableOperationResult> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        var terminal = result.Request;
        return WriteAsync(
            DurableJournalWriteOperation.RecordTerminal,
            result,
            terminal.Address,
            terminal.ExecutionContext.Authorization,
            terminal.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(terminal),
            SecurityEffect.Append,
            receipt => CommitTerminal(terminal, receipt),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordWaitingAsync(
        AuthorizedDurableRequest<DurableOperationWaiting> waiting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(waiting);
        var record = waiting.Request;
        return WriteAsync(
            DurableJournalWriteOperation.RecordWaiting,
            waiting,
            record.Address,
            record.ExecutionContext.Authorization,
            record.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(record),
            SecurityEffect.Mutate,
            receipt => CommitWaiting(record, receipt),
            cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The read is authorized and audited but unfenced: a recovering worker must be able to learn what happened
    /// before it seeks ownership, so presenting a fence it has not acquired is refused rather than required.
    /// A bare address carries no captured context, so authorization comes from the presented grant's own capture,
    /// which <see cref="AuthorizedDurableRequest{TRequest}"/> guarantees is present and which must describe
    /// <paramref name="address"/> exactly.
    /// </remarks>
    public async ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
        AuthorizedDurableRequest<DurableOperationAddress> address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        var operationAddress = address.Request;
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableJournalLoadEvidence,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, operationAddress.AgentId.ToString() },
                { AgentKitTagNames.SessionId, operationAddress.SessionId.ToString() },
                { AgentKitTagNames.RunId, operationAddress.RunId.ToString() },
                { AgentKitTagNames.OperationId, operationAddress.OperationId.ToString() },
            });
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Debug.Assert(
                address.Grant.Authorization is not null,
                "AuthorizedDurableRequest rejects a grant with no captured authorization at construction.");
            var (_, denial) = await EnforceAsync(
                address,
                operationAddress,
                address.Grant.Authorization,
                requiredFence: null,
                DurableJournalSecurityBinding.Fingerprint(operationAddress),
                SecurityOperationKind.StateRead,
                SecurityEffect.Observe,
                cancellationToken).ConfigureAwait(false);
            if (denial is not null)
            {
                FinishEvidenceLoad(activity, DurableJournalEvidenceOutcome.Denied, started, null);
                return new RecoveryEvidenceUnavailable(denial);
            }

            RecoveryEvidenceResult result;
            DurableJournalEvidenceOutcome outcome;
            lock (_gate)
            {
                if (_records.TryGetValue(operationAddress, out var existing))
                {
                    result = new RecoveryEvidenceLoaded(new RecoveryEvidence(
                        existing.Binding,
                        existing.State,
                        existing.SideEffectCertainty,
                        startDefinitelyAbsent: existing.State == DurableOperationState.Accepted,
                        terminalResultRecorded: existing.TerminalResultRecorded,
                        recordedResult: existing.TerminalResult,
                        notBefore: existing.NotBefore,
                        existing.LatestCheckpoint,
                        externalReference: existing.ExternalReference,
                        externalIdempotencyKey: existing.ExternalIdempotencyKey,
                        existing.LastWriterToken,
                        existing.Descriptor));
                    outcome = DurableJournalEvidenceOutcome.Loaded;
                }
                else
                {
                    result = new RecoveryEvidenceNotFound(operationAddress);
                    outcome = DurableJournalEvidenceOutcome.NotFound;
                }
            }

            FinishEvidenceLoad(activity, outcome, started, null);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishEvidenceLoad(activity, DurableJournalEvidenceOutcome.Cancelled, started, nameof(OperationCanceledException));
            throw;
        }
    }

    /// <summary>Commits one acceptance record under the journal's gate.</summary>
    /// <param name="operationStart">The complete acceptance declaration.</param>
    /// <param name="binding">The exact address and captured context retained by the record.</param>
    /// <param name="receipt">Evidence that this write's grant was consumed and audited.</param>
    /// <returns>The terminal write result.</returns>
    private DurableRecordResult CommitStart(
        DurableOperationStart operationStart,
        DurableOperationBinding binding,
        DurableJournalEnforcementReceipt receipt)
    {
        Debug.Assert(operationStart is not null, "An acceptance declaration is required.");
        Debug.Assert(binding is not null, "An exact binding is required.");
        var address = binding.Address;
        lock (_gate)
        {
            if (_records.TryGetValue(address, out var existing))
            {
                if (operationStart.FencingToken.Value < existing.LastWriterToken.Value)
                {
                    return Fenced(operationStart.FencingToken, existing.LastWriterToken);
                }
                if (existing.Binding != binding)
                {
                    return Failed("A different execution context is already recorded for this operation.", committed: false);
                }
                if (existing.State != DurableOperationState.Accepted)
                {
                    return Failed("This operation has already progressed past acceptance and cannot be restarted.", committed: true);
                }

                // Read the clock before mutating: if GetUtcNow() throws after the mutation, the caller
                // would see an exception for a write the journal had already committed, bypassing the
                // DurableRecordFailed(committed: …) channel meant to report exactly that.
                var recordedAt = _timeProvider.GetUtcNow();
                existing.LastWriterToken = operationStart.FencingToken;
                existing.Descriptor = operationStart.Descriptor;
                return Recorded(operationStart.FencingToken, recordedAt, receipt);
            }

            var recordedAtNew = _timeProvider.GetUtcNow();
            _records[address] = new DurableOperationRecord(binding, operationStart.FencingToken)
            {
                Descriptor = operationStart.Descriptor,
            };
            return Recorded(operationStart.FencingToken, recordedAtNew, receipt);
        }
    }

    /// <summary>Commits one state snapshot under the journal's gate.</summary>
    /// <param name="snapshot">The complete checkpoint replacing the operation's recorded state.</param>
    /// <param name="receipt">Evidence that this write's grant was consumed and audited.</param>
    /// <returns>The terminal write result.</returns>
    private DurableRecordResult CommitCheckpoint(DurableCheckpoint snapshot, DurableJournalEnforcementReceipt receipt)
    {
        Debug.Assert(snapshot is not null, "A checkpoint is required.");
        lock (_gate)
        {
            if (!_records.TryGetValue(snapshot.Address, out var existing))
            {
                return Failed("No accepted record exists for this operation.", committed: false);
            }
            if (snapshot.FencingToken.Value < existing.LastWriterToken.Value)
            {
                return Fenced(snapshot.FencingToken, existing.LastWriterToken);
            }
            if (existing.Binding != snapshot.Binding)
            {
                return Failed("A different execution context is already recorded for this operation.", committed: false);
            }
            if (IsTerminal(existing.State))
            {
                return Failed("This operation already has a terminal record; a further checkpoint cannot be recorded.", committed: true);
            }

            // Read the clock before mutating; see the identical comment in CommitStart.
            var recordedAt = _timeProvider.GetUtcNow();
            existing.State = DurableOperationState.EffectPending;
            existing.SideEffectCertainty = SideEffectCertainty.Unknown;
            existing.LatestCheckpoint = snapshot;
            existing.LastWriterToken = snapshot.FencingToken;
            return Recorded(snapshot.FencingToken, recordedAt, receipt);
        }
    }

    /// <summary>Commits one authoritative terminal record under the journal's gate.</summary>
    /// <param name="terminal">The terminal outcome and its truthful side-effect certainty.</param>
    /// <param name="receipt">Evidence that this write's grant was consumed and audited.</param>
    /// <returns>The terminal write result. Re-recording the identical result is idempotent.</returns>
    private DurableRecordResult CommitTerminal(DurableOperationResult terminal, DurableJournalEnforcementReceipt receipt)
    {
        Debug.Assert(terminal is not null, "A terminal result is required.");
        lock (_gate)
        {
            if (!_records.TryGetValue(terminal.Address, out var existing))
            {
                return Failed("No accepted record exists for this operation.", committed: false);
            }
            if (terminal.FencingToken.Value < existing.LastWriterToken.Value)
            {
                return Fenced(terminal.FencingToken, existing.LastWriterToken);
            }
            if (existing.Binding != terminal.Binding)
            {
                return Failed("A different execution context is already recorded for this operation.", committed: false);
            }
            if (existing.TerminalResult is { } recorded)
            {
                return recorded == terminal
                    ? Recorded(terminal.FencingToken, _timeProvider.GetUtcNow(), receipt)
                    : Failed("A different terminal result is already recorded for this operation.", committed: true);
            }

            // Read the clock before mutating; see the identical comment in CommitStart.
            var recordedAt = _timeProvider.GetUtcNow();
            existing.State = terminal.State;
            existing.SideEffectCertainty = terminal.SideEffectCertainty;
            existing.TerminalResult = terminal;
            existing.LastWriterToken = terminal.FencingToken;
            return Recorded(terminal.FencingToken, recordedAt, receipt);
        }
    }

    /// <summary>Commits one waiting record under the journal's gate.</summary>
    /// <param name="record">The complete waiting declaration and its wait condition.</param>
    /// <param name="receipt">Evidence that this write's grant was consumed and audited.</param>
    /// <returns>The terminal write result.</returns>
    private DurableRecordResult CommitWaiting(DurableOperationWaiting record, DurableJournalEnforcementReceipt receipt)
    {
        Debug.Assert(record is not null, "A waiting record is required.");
        lock (_gate)
        {
            if (!_records.TryGetValue(record.Address, out var existing))
            {
                return Failed("No accepted record exists for this operation.", committed: false);
            }
            if (record.FencingToken.Value < existing.LastWriterToken.Value)
            {
                return Fenced(record.FencingToken, existing.LastWriterToken);
            }
            if (existing.Binding != record.Binding)
            {
                return Failed("A different execution context is already recorded for this operation.", committed: false);
            }
            if (IsTerminal(existing.State))
            {
                return Failed("This operation already has a terminal record; waiting cannot be recorded.", committed: true);
            }

            var recordedAt = _timeProvider.GetUtcNow();
            existing.State = DurableOperationState.Waiting;
            existing.SideEffectCertainty = record.SideEffectCertainty;
            existing.NotBefore = record.NotBefore;
            existing.ExternalReference = record.ExternalReference;
            existing.ExternalIdempotencyKey = record.ExternalIdempotencyKey;
            existing.LastWriterToken = record.FencingToken;
            return Recorded(record.FencingToken, recordedAt, receipt);
        }
    }

    /// <summary>Authorizes, audits, and then runs one write body under shared cancellation handling and diagnostics.</summary>
    /// <typeparam name="TRequest">The immutable journal request shape.</typeparam>
    /// <param name="operation">The bounded write-stage dimension.</param>
    /// <param name="request">The protected request carrying the grant and enforcement intent.</param>
    /// <param name="address">The operation coordinates the write targets.</param>
    /// <param name="authorization">The capture the write must run under.</param>
    /// <param name="fencingToken">The ownership generation the write presents; it is also the required intent fence.</param>
    /// <param name="fingerprint">The canonical digest over the complete request.</param>
    /// <param name="effect">The protected effect this write performs.</param>
    /// <param name="body">The synchronous commit body executed under the journal's gate after authorization succeeds.</param>
    /// <param name="cancellationToken">Cancels the attempt before it commits.</param>
    /// <returns>The write body's terminal result, or a non-committed failure when authorization denied it.</returns>
    /// <remarks>
    /// Authorization strictly precedes the commit body, so a denial can never leave partial state. Diagnostics are
    /// observational: a listener or meter failure cannot change the committed outcome.
    /// </remarks>
    private async ValueTask<DurableRecordResult> WriteAsync<TRequest>(
        DurableJournalWriteOperation operation,
        AuthorizedDurableRequest<TRequest> request,
        DurableOperationAddress address,
        SecurityAuthorizationContext authorization,
        FencingToken fencingToken,
        InputFingerprint fingerprint,
        SecurityEffect effect,
        Func<DurableJournalEnforcementReceipt, DurableRecordResult> body,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        Debug.Assert(body is not null, "A write body is required.");
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableJournalWrite,
            ActivityKind.Internal,
            new ActivityTagsCollection { { AgentKitTagNames.DurableJournalOperation, operation.ToStableValue() } });
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (receipt, denial) = await EnforceAsync(
                request,
                address,
                authorization,
                fencingToken,
                fingerprint,
                SecurityOperationKind.StateMutation,
                effect,
                cancellationToken).ConfigureAwait(false);
            if (receipt is not { } enforcement)
            {
                var refused = Failed(denial ?? "The durable journal operation was not authorized.", committed: false);
                FinishWrite(activity, operation, DurableJournalWriteOutcome.Denied, started, null);
                return refused;
            }

            var result = body(enforcement);
            FinishWrite(activity, operation, Classify(result), started, null);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishWrite(activity, operation, DurableJournalWriteOutcome.Cancelled, started, nameof(OperationCanceledException));
            throw;
        }
        catch (Exception exception)
        {
            FinishWrite(activity, operation, DurableJournalWriteOutcome.Failed, started, ErrorType(exception));
            throw;
        }
    }

    /// <summary>Classifies a committed write outcome for diagnostics only.</summary>
    private static DurableJournalWriteOutcome Classify(DurableRecordResult result)
    {
        Debug.Assert(result is not null, "A terminal write result is required.");
        return result switch
        {
            DurableRecorded => DurableJournalWriteOutcome.Recorded,
            DurableRecordFenced => DurableJournalWriteOutcome.Fenced,
            _ => DurableJournalWriteOutcome.Failed,
        };
    }

    /// <summary>Identifies whether a lifecycle state already carries a terminal record.</summary>
    private static bool IsTerminal(DurableOperationState state) => state is
        DurableOperationState.OutcomeReady or DurableOperationState.Completed or DurableOperationState.Faulted;

    private static DurableRecorded Recorded(
        FencingToken token,
        DateTimeOffset recordedAt,
        DurableJournalEnforcementReceipt receipt) => new(token, recordedAt, receipt);

    private static DurableRecordFenced Fenced(FencingToken presented, FencingToken current) => new(presented, current);

    private static string ErrorType(Exception exception)
    {
        Debug.Assert(exception is not null, "Only observed exceptions are classified.");
        return exception.GetType().FullName ?? exception.GetType().Name;
    }

    private static DurableRecordFailed Failed(string safeMessage, bool committed) => new(safeMessage, committed);

    private void FinishWrite(Activity? activity, DurableJournalWriteOperation operation, DurableJournalWriteOutcome outcome, long? started, string? errorType)
    {
        var operationValue = operation.ToStableValue();
        var outcomeValue = outcome.ToStableValue();
        SafeSetActivity(activity, current =>
        {
            if (outcome == DurableJournalWriteOutcome.Recorded)
            {
                current.SetSuccessful(outcomeValue);
            }
            else
            {
                current.SetFailed(outcomeValue, errorType ?? outcomeValue);
            }
        });
        try
        {
            if (errorType is not null && outcome == DurableJournalWriteOutcome.Failed)
            {
                DurableJournalLog.WriteFailed(_logger, operationValue, errorType);
            }
            else if (errorType is not null && outcome == DurableJournalWriteOutcome.Cancelled)
            {
                DurableJournalLog.WriteCancelled(_logger, operationValue);
            }
            else if (outcome == DurableJournalWriteOutcome.Denied)
            {
                DurableJournalLog.WriteDenied(_logger, operationValue);
            }
            else
            {
                DurableJournalLog.WriteCompleted(_logger, operationValue, outcomeValue);
            }
        }
        catch
        {
            // Logging is observational and cannot alter the committed write outcome.
        }

        var elapsed = TryGetElapsedTime(started);
        try
        {
            DurableJournalMetrics.RecordWrite(operation, outcome, elapsed);
        }
        catch
        {
            // Metrics are observational and cannot alter the committed write outcome.
        }
    }

    private void FinishEvidenceLoad(Activity? activity, DurableJournalEvidenceOutcome outcome, long? started, string? errorType)
    {
        var outcomeValue = outcome.ToStableValue();
        SafeSetActivity(activity, current =>
        {
            if (outcome is DurableJournalEvidenceOutcome.Loaded or DurableJournalEvidenceOutcome.NotFound)
            {
                current.SetSuccessful(outcomeValue);
            }
            else
            {
                current.SetFailed(outcomeValue, errorType ?? outcomeValue);
            }
        });
        try
        {
            if (outcome == DurableJournalEvidenceOutcome.Cancelled)
            {
                DurableJournalLog.EvidenceLoadCancelled(_logger);
            }
            else
            {
                DurableJournalLog.EvidenceLoadCompleted(_logger, outcomeValue);
            }
        }
        catch
        {
            // Logging is observational and cannot alter the loaded evidence.
        }

        var elapsed = TryGetElapsedTime(started);
        try
        {
            DurableJournalMetrics.RecordEvidenceLoad(outcome, elapsed);
        }
        catch
        {
            // Metrics are observational and cannot alter the loaded evidence.
        }
    }

    private long? TryGetTimestamp()
    {
        try
        {
            return _timeProvider.GetTimestamp();
        }
        catch
        {
            return null;
        }
    }

    private TimeSpan? TryGetElapsedTime(long? started)
    {
        if (started is not { } timestamp)
        {
            return null;
        }

        try
        {
            return _timeProvider.GetElapsedTime(timestamp);
        }
        catch
        {
            return null;
        }
    }

    private static void SafeSetActivity(Activity? activity, Action<Activity?> action)
    {
        Debug.Assert(action is not null, "Only package-owned activity updates are applied.");
        try
        {
            action(activity);
        }
        catch
        {
            // Activity listeners cannot alter a committed journal outcome.
        }
    }
}
