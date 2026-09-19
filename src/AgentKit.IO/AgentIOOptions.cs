// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Configures bounds applied by first-party <c>AgentKit.IO</c> input coordination components.</summary>
/// <remarks>
/// This is a mutable options type bound through <see cref="IOptions{TOptions}"/>. Hosts configure it with
/// <c>services.Configure&lt;AgentIOOptions&gt;(...)</c>; a component that consumes it validates the bound values
/// eagerly when constructed, so a property left in an invalid state fails at composition rather than mid-run.
/// </remarks>
public sealed class AgentIOOptions
{
    /// <summary>Gets or sets the maximum number of durably admitted, not-yet-promoted inputs a lane retains at once.</summary>
    /// <value>A positive per-lane ceiling, defaulting to 8. Admission beyond this bound reports queue capacity exceeded.</value>
    public int MaximumPendingInputsPerLane { get; set; } = 8;

    /// <summary>Gets or sets the maximum number of follow-up inputs promoted at one otherwise-idle boundary.</summary>
    /// <value>A positive per-boundary ceiling, defaulting to 1, so a busy producer cannot consume an unbounded queue in a single run.</value>
    public int MaximumFollowUpPromotionsPerIdleBoundary { get; set; } = 1;

    /// <summary>Gets or sets the maximum number of simultaneously registered live subscribers for one run.</summary>
    /// <value>A positive count, defaulting to 32; a released or disconnected subscription frees its registration immediately.</value>
    public int MaximumSubscriptions { get; set; } = 32;

    /// <summary>Gets or sets the maximum event count retained for each live subscriber.</summary>
    /// <value>A positive count, defaulting to 256; a saturated recipient is disconnected explicitly instead of blocking the producer.</value>
    public int CapacityPerSubscription { get; set; } = 256;

    /// <summary>Gets or sets how long the first-party <see cref="DefaultOutputBackpressurePolicy"/> waits for a best-effort sink before dropping its delivery.</summary>
    /// <value>A positive duration, defaulting to 5 seconds.</value>
    public TimeSpan MaximumBestEffortSinkWait { get; set; } = TimeSpan.FromSeconds(5);
}
