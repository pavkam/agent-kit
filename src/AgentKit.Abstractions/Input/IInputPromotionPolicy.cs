// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects eligible admitted input without mutating durable session state.</summary>
public interface IInputPromotionPolicy
{
    /// <summary>Creates a complete deterministic bounded selection or a typed rejection.</summary>
    /// <param name="context">Captured eligible input and ownership evidence.</param><param name="cancellationToken">Cancellation observed during selection.</param>
    /// <returns>A synchronously completable planning result.</returns>
    public ValueTask<InputPromotionPlanningResult> PlanAsync(InputPromotionContext context, CancellationToken cancellationToken = default);
}
