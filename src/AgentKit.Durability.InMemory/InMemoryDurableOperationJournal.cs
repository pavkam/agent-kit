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
/// <see cref="DurableOperationState.EffectPending"/> after a checkpoint, and the caller-supplied terminal state after
/// a terminal record — and derives <see cref="RecoveryEvidence.StartDefinitelyAbsent"/> and
/// <see cref="SideEffectCertainty"/> from that same computed state rather than trusting a separate caller claim.
/// </para>
/// <para>
/// <b>This journal does not perform grant consumption or audit dispatch.</b> Every one of
/// <see cref="IDurableOperationJournal"/>'s four methods receives only the durable value being recorded — none takes
/// a live <c>SecurityGrant</c> or <c>SecurityEnforcementIntent</c> the way <c>ISessionStore</c>'s protected methods do
/// through <c>AuthorizedSessionStoreRequest&lt;TRequest&gt;</c>. <see cref="LoadEvidenceAsync"/> receives only a bare
/// <see cref="DurableOperationAddress"/>, with no authorization evidence at all. Implementing the "protected
/// operation" behavior <see cref="IDurableOperationJournal"/>'s own remarks describe is not possible against the
/// current interface shape; this is a specification gap, not an omission in this adapter.
/// </para>
/// <para>
/// This journal also cannot record <see cref="DurableOperationState.Waiting"/> or an
/// <see cref="ExternalOperationReference"/>: no method on <see cref="IDurableOperationJournal"/> accepts either as an
/// argument, so <see cref="RecoveryEvidence.ExternalReference"/> is always <see langword="null"/> from this adapter.
/// </para>
/// </remarks>
public sealed class InMemoryDurableOperationJournal: IDurableOperationJournal
{
    private readonly Lock _gate = new();
    private readonly Dictionary<DurableOperationAddress, DurableOperationRecord> _records = [];
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemoryDurableOperationJournal> _logger;

    /// <summary>Initializes an empty journal with a replaceable clock.</summary>
    /// <param name="timeProvider">The non-null injected clock used for commit timestamps and elapsed measurement.</param>
    /// <param name="logger">The optional content-free structured logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public InMemoryDurableOperationJournal(TimeProvider timeProvider, ILogger<InMemoryDurableOperationJournal>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryDurableOperationJournal>.Instance;
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordStartAsync(DurableOperationStart start, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        return WriteAsync(DurableJournalWriteOperation.RecordStart, () =>
        {
            var address = start.Descriptor.Address;
            var binding = start.Descriptor.Binding;
            lock (_gate)
            {
                if (_records.TryGetValue(address, out var existing))
                {
                    if (start.FencingToken.Value < existing.LastWriterToken.Value)
                    {
                        return Fenced(start.FencingToken, existing.LastWriterToken);
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
                    existing.LastWriterToken = start.FencingToken;
                    return Recorded(start.FencingToken, recordedAt);
                }

                var recordedAtNew = _timeProvider.GetUtcNow();
                _records[address] = new DurableOperationRecord(binding, start.FencingToken);
                return Recorded(start.FencingToken, recordedAtNew);
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordCheckpointAsync(DurableCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return WriteAsync(DurableJournalWriteOperation.RecordCheckpoint, () =>
        {
            lock (_gate)
            {
                if (!_records.TryGetValue(checkpoint.Address, out var existing))
                {
                    return Failed("No accepted record exists for this operation.", committed: false);
                }
                if (checkpoint.FencingToken.Value < existing.LastWriterToken.Value)
                {
                    return Fenced(checkpoint.FencingToken, existing.LastWriterToken);
                }
                if (existing.Binding != checkpoint.Binding)
                {
                    return Failed("A different execution context is already recorded for this operation.", committed: false);
                }
                if (IsTerminal(existing.State))
                {
                    return Failed("This operation already has a terminal record; a further checkpoint cannot be recorded.", committed: true);
                }

                // Read the clock before mutating; see the identical comment in RecordStartAsync.
                var recordedAt = _timeProvider.GetUtcNow();
                existing.State = DurableOperationState.EffectPending;
                existing.SideEffectCertainty = SideEffectCertainty.Unknown;
                existing.LatestCheckpoint = checkpoint;
                existing.LastWriterToken = checkpoint.FencingToken;
                return Recorded(checkpoint.FencingToken, recordedAt);
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordTerminalAsync(DurableOperationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return WriteAsync(DurableJournalWriteOperation.RecordTerminal, () =>
        {
            lock (_gate)
            {
                if (!_records.TryGetValue(result.Address, out var existing))
                {
                    return Failed("No accepted record exists for this operation.", committed: false);
                }
                if (result.FencingToken.Value < existing.LastWriterToken.Value)
                {
                    return Fenced(result.FencingToken, existing.LastWriterToken);
                }
                if (existing.Binding != result.Binding)
                {
                    return Failed("A different execution context is already recorded for this operation.", committed: false);
                }
                if (existing.TerminalResult is { } recorded)
                {
                    return recorded == result
                        ? Recorded(result.FencingToken, _timeProvider.GetUtcNow())
                        : Failed("A different terminal result is already recorded for this operation.", committed: true);
                }

                // Read the clock before mutating; see the identical comment in RecordStartAsync.
                var recordedAt = _timeProvider.GetUtcNow();
                existing.State = result.State;
                existing.SideEffectCertainty = result.SideEffectCertainty;
                existing.TerminalResult = result;
                existing.LastWriterToken = result.FencingToken;
                return Recorded(result.FencingToken, recordedAt);
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(DurableOperationAddress address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableJournalLoadEvidence,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, address.AgentId.ToString() },
                { AgentKitTagNames.SessionId, address.SessionId.ToString() },
                { AgentKitTagNames.RunId, address.RunId.ToString() },
                { AgentKitTagNames.OperationId, address.OperationId.ToString() },
            });
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecoveryEvidenceResult result;
            DurableJournalEvidenceOutcome outcome;
            lock (_gate)
            {
                if (_records.TryGetValue(address, out var existing))
                {
                    result = new RecoveryEvidenceLoaded(new RecoveryEvidence(
                        existing.Binding,
                        existing.State,
                        existing.SideEffectCertainty,
                        startDefinitelyAbsent: existing.State == DurableOperationState.Accepted,
                        terminalResultRecorded: existing.TerminalResultRecorded,
                        existing.LatestCheckpoint,
                        externalReference: null,
                        externalIdempotencyKey: null,
                        existing.LastWriterToken));
                    outcome = DurableJournalEvidenceOutcome.Loaded;
                }
                else
                {
                    result = new RecoveryEvidenceNotFound(address);
                    outcome = DurableJournalEvidenceOutcome.NotFound;
                }
            }

            FinishEvidenceLoad(activity, outcome, started, null);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishEvidenceLoad(activity, DurableJournalEvidenceOutcome.Cancelled, started, nameof(OperationCanceledException));
            throw;
        }
    }

    /// <summary>Runs one write body under shared cancellation handling and diagnostics.</summary>
    /// <param name="operation">The bounded write-stage dimension.</param>
    /// <param name="cancellationToken">Cancels the attempt before it runs.</param>
    /// <param name="body">The synchronous write body executed under the journal's gate.</param>
    /// <returns>The write body's terminal result.</returns>
    private ValueTask<DurableRecordResult> WriteAsync(
        DurableJournalWriteOperation operation,
        Func<DurableRecordResult> body,
        CancellationToken cancellationToken)
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
            var result = body();
            FinishWrite(activity, operation, Classify(result), started, null);
            return ValueTask.FromResult(result);
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

    private static DurableRecorded Recorded(FencingToken token, DateTimeOffset recordedAt) => new(token, recordedAt);

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
            if (outcome != DurableJournalEvidenceOutcome.Cancelled)
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
