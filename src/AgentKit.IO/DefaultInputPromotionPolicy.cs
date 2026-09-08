// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

using System.Collections.Immutable;

/// <summary>Implements deterministic steering and single-follow-up promotion over a captured eligible snapshot.</summary>
/// <remarks>The policy performs no durable mutation. It rejects a selection bound that cannot include every input required by the boundary instead of silently truncating work.</remarks>
internal sealed class DefaultInputPromotionPolicy: IInputPromotionPolicy
{
    /// <inheritdoc/>
    public ValueTask<InputPromotionPlanningResult> PlanAsync(
        InputPromotionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var ordered = context.Eligible.ToArray();
        Array.Sort(ordered, static (left, right) => left.AdmittedSequence.Value.CompareTo(right.AdmittedSequence.Value));
        var selected = ImmutableArray.CreateBuilder<AdmissionId>();
        if (context.Boundary == PromotionBoundary.OtherwiseIdle)
        {
            var followUp = ordered.FirstOrDefault(static input => input.EffectivePayload.Delivery == InputDelivery.FollowUp);
            if (followUp is not null)
            {
                selected.Add(followUp.AdmissionId);
            }
        }

        foreach (var input in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.EffectivePayload.Delivery == InputDelivery.Steer)
            {
                selected.Add(input.AdmissionId);
            }
        }

        if (selected.Count > context.MaximumPromotions)
        {
            return ValueTask.FromResult<InputPromotionPlanningResult>(new InputPromotionPlanRejected(
                InputPromotionPlanRejectionKind.SelectionLimitExceeded,
                selected.Count,
                context.MaximumPromotions,
                "The promotion bound cannot include every input required at this boundary."));
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
            context.TargetTurnId,
            selected.ToImmutable());
        return ValueTask.FromResult<InputPromotionPlanningResult>(new InputPromotionPlan(snapshot));
    }
}
