// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>The in-memory reservation handle returned by <see cref="InMemoryBudgetScope"/>.</summary>
/// <remarks>A started reservation is never released by disposal or expiry and remains reconcilable.</remarks>
internal sealed class InMemoryBudgetReservation: IBudgetReservation
{
    private const int _stateOpen = 0;
    private const int _stateStarted = 1;
    private const int _stateCommitted = 2;
    private const int _stateReleased = 3;
    private int _state;
    private long _revision;
    private BudgetCorrectionResult? _lastCorrection;
    private InMemoryBudgetReservation? _parentReservation;
    private readonly ILogger<InMemoryBudgetScope> _logger;

    /// <summary>Initializes one local level of a hierarchical reservation.</summary>
    /// <param name="id">The reservation identity.</param><param name="scope">The owning scope.</param>
    /// <param name="dimension">The reserved dimension.</param><param name="reserved">The reserved amount.</param>
    /// <param name="unit">The unit of the reserved amount.</param>
    /// <param name="expiresAt">The unstarted expiry.</param><param name="idempotencyKey">The item key.</param>
    /// <param name="batch">The ordered batch binding.</param><param name="logger">The structured logger.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    public InMemoryBudgetReservation(
        BudgetReservationId id, InMemoryBudgetScope scope, BudgetDimension dimension,
        decimal reserved, BudgetUnit unit, DateTimeOffset expiresAt, IdempotencyKey idempotencyKey,
        InMemoryBudgetBatchRecord batch, ILogger<InMemoryBudgetScope> logger)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(logger);
        Id = id;
        Scope = scope;
        Dimension = dimension;
        Reserved = reserved;
        Unit = unit;
        ExpiresAt = expiresAt;
        IdempotencyKey = idempotencyKey;
        Batch = batch;
        _logger = logger;
    }

    /// <inheritdoc/>
    public BudgetReservationId Id { get; }
    /// <summary>Gets the owning scope.</summary>
    public InMemoryBudgetScope Scope { get; }
    /// <inheritdoc/>
    public BudgetScopeId ScopeId => Scope.Id;
    /// <inheritdoc/>
    public BudgetDimension Dimension { get; }
    /// <inheritdoc/>
    public decimal Reserved { get; }
    /// <summary>Gets the unit of the reserved amount.</summary>
    public BudgetUnit Unit { get; }
    /// <summary>Gets the expiry applied only while unstarted.</summary>
    public DateTimeOffset ExpiresAt { get; }
    /// <summary>Gets the item idempotency key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
    /// <summary>Gets the ordered batch binding.</summary>
    public InMemoryBudgetBatchRecord Batch { get; }
    /// <summary>Gets whether the reservation remains unstarted.</summary>
    public bool IsOpen => Volatile.Read(ref _state) == _stateOpen;
    /// <summary>Gets the currently recorded actual usage.</summary>
    public decimal Actual { get; private set; }

    /// <summary>Attaches the immediately enclosing ancestor reservation.</summary>
    /// <param name="parentReservation">The ancestor reservation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parentReservation"/> is null.</exception>
    public void AttachParent(InMemoryBudgetReservation parentReservation)
    {
        ArgumentNullException.ThrowIfNull(parentReservation);
        _parentReservation = parentReservation;
    }

    /// <inheritdoc/>
    public ValueTask<BudgetStartResult> MarkStartedAsync(CancellationToken cancellationToken = default)
    {
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.BudgetStart);
        _ = activity?.SetTag(AgentKitTagNames.BudgetScopeId, ScopeId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.BudgetDimension, Dimension.ToString());
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = Scope.MarkStarted(this, cancellationToken);
            var outcome = result is BudgetStarted ? "started" : "start_rejected";
            if (result is BudgetStarted)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, nameof(BudgetLimitFailure));
            }

            BudgetLog.StartCompleted(_logger, ScopeId, Dimension, outcome);
            BudgetMetrics.RecordStart(outcome, Dimension);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            BudgetLog.StartCompleted(_logger, ScopeId, Dimension, "cancelled");
            BudgetMetrics.RecordStart("cancelled", Dimension);
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            BudgetLog.StartFailed(_logger, ScopeId, Dimension, exception.GetType().FullName ?? exception.GetType().Name);
            BudgetMetrics.RecordStart("failed", Dimension);
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<BudgetCommitResult> CommitAsync(decimal actual, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actual);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.BudgetCommit);
        _ = activity?.SetTag(AgentKitTagNames.BudgetScopeId, ScopeId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.BudgetDimension, Dimension.ToString());
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = Scope.Commit(this, actual, cancellationToken);
            var outcome = result.Overrun > 0m ? "committed_overrun" : "committed";
            activity.SetSuccessful(outcome);
            BudgetLog.SettlementCompleted(_logger, ScopeId, Dimension, outcome);
            BudgetMetrics.RecordSettlement(outcome, Dimension);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            BudgetLog.SettlementCompleted(_logger, ScopeId, Dimension, "cancelled");
            BudgetMetrics.RecordSettlement("cancelled", Dimension);
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            BudgetLog.SettlementFailed(_logger, ScopeId, Dimension, exception.GetType().FullName ?? exception.GetType().Name);
            BudgetMetrics.RecordSettlement("failed", Dimension);
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<BudgetCorrectionResult> CorrectAsync(
        decimal correctedActual, long revision, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(correctedActual);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.BudgetCorrection);
        _ = activity?.SetTag(AgentKitTagNames.BudgetScopeId, ScopeId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.BudgetDimension, Dimension.ToString());
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = Scope.Correct(this, correctedActual, revision, cancellationToken);
            activity.SetSuccessful("corrected");
            BudgetLog.CorrectionCompleted(_logger, ScopeId, Dimension, "corrected");
            BudgetMetrics.RecordCorrection("corrected", Dimension);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            BudgetLog.CorrectionCompleted(_logger, ScopeId, Dimension, "cancelled");
            BudgetMetrics.RecordCorrection("cancelled", Dimension);
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            BudgetLog.CorrectionFailed(_logger, ScopeId, Dimension, exception.GetType().FullName ?? exception.GetType().Name);
            BudgetMetrics.RecordCorrection("failed", Dimension);
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Scope.Release(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>Transitions this reservation hierarchy to started under the shared gate.</summary>
    /// <returns>The idempotent start result.</returns>
    internal BudgetStartResult MarkHierarchyStartedLocked()
    {
        var wasAlreadyStarted = _state == _stateStarted;
        if (_state == _stateReleased)
        {
            return new BudgetStartRejected(CreateStartFailure());
        }
        if (_state == _stateCommitted)
        {
            throw new InvalidOperationException("A committed reservation cannot be started.");
        }

        for (var current = this; current is not null; current = current._parentReservation)
        {
            if (current._state is not (_stateOpen or _stateStarted))
            {
                throw new InvalidOperationException("The reservation hierarchy is not startable.");
            }
        }

        for (var current = this; current is not null; current = current._parentReservation)
        {
            if (current._state == _stateOpen)
            {
                current._state = _stateStarted;
            }
        }
        return new BudgetStarted(Id, wasAlreadyStarted);
    }

    /// <summary>Commits this reservation hierarchy under the shared gate.</summary>
    /// <param name="actual">The truthful actual usage.</param><returns>The caller-visible settlement.</returns>
    internal BudgetCommitResult CommitHierarchyLocked(decimal actual)
    {
        if (_state != _stateStarted)
        {
            throw new InvalidOperationException("The reservation must be started before it can be committed.");
        }

        for (var current = this; current is not null; current = current._parentReservation)
        {
            if (current._state != _stateStarted)
            {
                throw new InvalidOperationException("The reservation hierarchy is not committable.");
            }
        }

        BudgetCommitResult? result = null;
        for (var current = this; current is not null; current = current._parentReservation)
        {
            current.Actual = actual;
            var local = current.Scope.CommitLocalLocked(current, actual);
            result ??= local;
            current._revision = 0;
            current._state = _stateCommitted;
        }
        return result!;
    }

    /// <summary>Corrects this reservation hierarchy under the shared gate.</summary>
    /// <param name="correctedActual">The replacement actual.</param><param name="revision">The correction revision.</param>
    /// <returns>The caller-visible correction result.</returns>
    internal BudgetCorrectionResult CorrectHierarchyLocked(decimal correctedActual, long revision)
    {
        if (_state != _stateCommitted)
        {
            throw new InvalidOperationException("Only a committed reservation can be corrected.");
        }

        if (revision < _revision)
        {
            throw new InvalidOperationException("Correction revisions must increase monotonically.");
        }

        if (revision == _revision)
        {
            return correctedActual != Actual
                ? throw new InvalidOperationException("The correction revision is already bound to different accounting.")
                : _lastCorrection!;
        }
        var previous = Actual;
        var previousRevision = _revision;
        for (var current = this; current is not null; current = current._parentReservation)
        {
            if (current._state != _stateCommitted || current._revision != previousRevision)
            {
                throw new InvalidOperationException("The reservation hierarchy has inconsistent correction state.");
            }
        }

        for (var current = this; current is not null; current = current._parentReservation)
        {
            current.Scope.CorrectLocalLocked(current, correctedActual);
            current.Actual = correctedActual;
            current._revision = revision;
        }
        _lastCorrection = new BudgetCorrectionResult(Id, previous, correctedActual, revision);
        return _lastCorrection;
    }

    /// <summary>Releases this reservation hierarchy only while it remains unstarted.</summary>
    internal void ReleaseHierarchyLocked()
    {
        if (_state != _stateOpen)
        {
            return;
        }

        for (var current = this; current is not null; current = current._parentReservation)
        {
            if (current._state != _stateOpen)
            {
                throw new InvalidOperationException("The reservation hierarchy has inconsistent release state.");
            }
        }

        for (var current = this; current is not null; current = current._parentReservation)
        {
            current._state = _stateReleased;
            current.Scope.ReleaseLocalLocked(current);
        }
    }

    private BudgetLimitFailure CreateStartFailure()
    {
        var limit = Scope.Limits.GetValueOrDefault(Dimension);
        return new BudgetLimitFailure(
            ScopeId,
            Dimension,
            BudgetLimitKind.Hard,
            limit?.Value ?? Reserved,
            limit?.Value ?? Reserved,
            Reserved,
            Unit,
            "The reservation expired or was released before its effect started.");
    }
}
