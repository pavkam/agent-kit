// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns protected, optimistic current-plan observation and mutation for one session.</summary>
public interface IPlanStateStore
{
    /// <summary>Gets the component audience for plan-state grants.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Reads the current plan after consuming exact observation authority.</summary>
    /// <param name="request">The protected read.</param>
    /// <param name="cancellationToken">Cancels before a terminal result.</param>
    /// <returns>The current, missing, denied, or failed result.</returns>
    public ValueTask<PlanStateResult> ReadAsync(PlanReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Replaces the current plan after consuming exact mutation authority.</summary>
    /// <param name="request">The protected optimistic replacement.</param>
    /// <param name="cancellationToken">Cancels before a terminal result.</param>
    /// <returns>The current, conflict, denied, or failed result.</returns>
    public ValueTask<PlanStateResult> ReplaceAsync(PlanReplaceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes one item status after consuming exact mutation authority.</summary>
    /// <param name="request">The protected optimistic status transition.</param>
    /// <param name="cancellationToken">Cancels before a terminal result.</param>
    /// <returns>The current, missing, conflict, denied, or failed result.</returns>
    public ValueTask<PlanStateResult> SetStatusAsync(PlanStatusRequest request, CancellationToken cancellationToken = default);
}
