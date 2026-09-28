// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Records bounded durability-runtime measurements without payload or identity dimensions.</summary>
/// <remarks>
/// Instruments are process-wide and created once. Dimensions are limited to the finite
/// <see cref="DurableCoordinatorStage"/>, <see cref="DurableCoordinatorOutcome"/>, and
/// <see cref="DurableEventDispatchOutcome"/> vocabularies, so no measurement can carry a session, run, or operation
/// identity.
/// </remarks>
internal static class DurabilityMetrics
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.DurableOperationCount, unit: "{operation}",
        description: "Number of terminal coordinated durable-operation stage outcomes.");
    private static readonly Histogram<double> _operationDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.DurableOperationDuration, "s",
        "Duration of coordinated durable-operation stages.");
    private static readonly Counter<long> _eventDispatches = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.DurableEventDispatchCount, unit: "{dispatch}",
        description: "Number of terminal durable execution event sink invocations.");

    /// <summary>Records one terminal coordinator stage outcome and its optional measured duration.</summary>
    /// <param name="stage">The finite coordinator stage dimension.</param>
    /// <param name="outcome">The finite terminal outcome dimension.</param>
    /// <param name="elapsed">
    /// The nonnegative measured duration, or <see langword="null"/> when the injected clock could not produce one.
    /// A missing measurement is omitted rather than reported as zero.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="stage"/> or <paramref name="outcome"/> is undefined, or a present <paramref name="elapsed"/> is negative.
    /// </exception>
    internal static void RecordStage(DurableCoordinatorStage stage, DurableCoordinatorOutcome outcome, TimeSpan? elapsed = null)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(stage);
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }

        TagList tags = default;
        tags.Add(AgentKitTagNames.DurableOperationStage, stage.ToStableValue());
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        _operations.Add(1, tags);
        if (elapsed is { } measured)
        {
            _operationDuration.Record(measured.TotalSeconds, tags);
        }
    }

    /// <summary>Records one terminal durable execution event sink invocation.</summary>
    /// <param name="outcome">The finite terminal dispatch outcome dimension.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
    internal static void RecordEventDispatch(DurableEventDispatchOutcome outcome)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        _eventDispatches.Add(1, tags);
    }
}
