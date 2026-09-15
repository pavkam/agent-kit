// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

using System.Collections.Immutable;

/// <summary>Implements deterministic steering and single-follow-up promotion over a captured eligible snapshot.</summary>
/// <remarks>The policy performs no durable mutation. It rejects a selection bound that cannot include every input required by the boundary instead of silently truncating work.</remarks>
internal sealed class DefaultInputPromotionPolicy: IInputPromotionPolicy
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultInputPromotionPolicy> _logger;

    /// <summary>Initializes deterministic planning with replaceable observation dependencies.</summary>
    /// <param name="timeProvider">The injected clock used only for elapsed measurement.</param>
    /// <param name="logger">The optional content-free structured logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public DefaultInputPromotionPolicy(TimeProvider timeProvider, ILogger<DefaultInputPromotionPolicy>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultInputPromotionPolicy>.Instance;
    }

    /// <inheritdoc/>
    public ValueTask<InputPromotionPlanningResult> PlanAsync(
        InputPromotionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.InputPromotionPlan,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, context.AgentId.ToString() },
                { AgentKitTagNames.SessionId, context.SessionId.ToString() },
                { AgentKitTagNames.RunId, context.ExpectedOperation.RunId.ToString() },
                { AgentKitTagNames.OperationId, context.ExpectedOperation.OperationId.ToString() },
                { AgentKitTagNames.InputPromotionBoundary, context.Boundary.ToString() },
                { AgentKitTagNames.ExecutionLaneId, context.ExecutionLaneId.ToString() },
            });
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = Plan(context, cancellationToken);
            var outcome = result is InputPromotionPlan
                ? InputPromotionPlanOutcome.Planned
                : InputPromotionPlanOutcome.Rejected;
            SafeSetActivity(() =>
            {
                if (result is InputPromotionPlan)
                {
                    activity.SetSuccessful(outcome.ToStableValue());
                }
                else
                {
                    activity.SetFailed(outcome.ToStableValue(), nameof(InputPromotionPlanRejected));
                }
            });
            SafeObserve(context.Boundary, outcome, started, null);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeSetActivity(() => activity.SetFailed(InputPromotionPlanOutcome.Cancelled.ToStableValue(), nameof(OperationCanceledException)));
            SafeObserve(context.Boundary, InputPromotionPlanOutcome.Cancelled, started, null);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            SafeSetActivity(() => activity.SetFailed(InputPromotionPlanOutcome.Failed.ToStableValue(), errorType));
            SafeObserve(context.Boundary, InputPromotionPlanOutcome.Failed, started, errorType);
            throw;
        }
    }

    private static InputPromotionPlanningResult Plan(InputPromotionContext context, CancellationToken cancellationToken)
    {
        Debug.Assert(context is not null, "The public policy boundary validates the context.");

        var requiredSteers = 0;
        AdmittedInput? oldestFollowUp = null;
        foreach (var input in context.Eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.EffectivePayload.Delivery == InputDelivery.Steer)
            {
                requiredSteers++;
            }
            else if (context.Boundary == PromotionBoundary.OtherwiseIdle
                && (oldestFollowUp is null || input.AdmittedSequence.Value < oldestFollowUp.AdmittedSequence.Value))
            {
                oldestFollowUp = input;
            }
        }

        var requiredCount = requiredSteers + (oldestFollowUp is null ? 0 : 1);
        if (requiredCount == 0)
        {
            // Nothing qualifies at this boundary; that is the common case for a steering boundary with only
            // follow-up input queued, and it is a typed planning outcome rather than a snapshot invariant failure.
            return new InputPromotionPlanRejected(
                InputPromotionPlanRejectionKind.NothingEligible,
                requiredCount,
                context.MaximumPromotions,
                "No eligible input qualifies for promotion at this boundary.");
        }

        if (requiredCount > context.MaximumPromotions)
        {
            return new InputPromotionPlanRejected(
                InputPromotionPlanRejectionKind.SelectionLimitExceeded,
                requiredCount,
                context.MaximumPromotions,
                "The promotion bound cannot include every input required at this boundary.");
        }

        var steers = context.Eligible
            .Where(static input => input.EffectivePayload.Delivery == InputDelivery.Steer)
            .OrderBy(static input => input.AdmittedSequence.Value);
        var selected = ImmutableArray.CreateBuilder<AdmissionId>();
        if (oldestFollowUp is not null)
        {
            selected.Add(oldestFollowUp.AdmissionId);
        }
        foreach (var steer in steers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            selected.Add(steer.AdmissionId);
        }

        var snapshot = new InputPromotionSnapshot(
            context.AgentId,
            context.SessionId,
            context.ExecutionLaneId,
            context.ExpectedOperation,
            context.OperationStateRevision,
            context.BranchCursor,
            context.CutoffSequence,
            context.ExpectedVersion,
            context.ExpectedFencingToken,
            context.Boundary,
            context.PreviousTurnId,
            context.TargetTurnId,
            selected.ToImmutable());
        return new InputPromotionPlan(snapshot);
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

    private void SafeObserve(PromotionBoundary boundary, InputPromotionPlanOutcome outcome, long? started, string? errorType)
    {
        var outcomeValue = outcome.ToStableValue();
        try
        {
            if (outcome == InputPromotionPlanOutcome.Cancelled)
            {
                IOLog.PromotionPlanCancelled(_logger, boundary);
            }
            else if (errorType is not null)
            {
                IOLog.PromotionPlanFailed(_logger, boundary, errorType);
            }
            else
            {
                IOLog.PromotionPlanCompleted(_logger, boundary, outcomeValue);
            }
        }
        catch
        {
            // Logging is observational and cannot alter the promotion decision or metric emission.
        }

        var elapsed = TryGetElapsedTime(started);
        try
        {
            IOMetrics.RecordPromotionPlan(boundary, outcome, elapsed);
        }
        catch
        {
            // Metrics are observational and cannot alter the promotion decision.
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

    private static void SafeSetActivity(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Activity listeners cannot alter the promotion decision.
        }
    }
}
