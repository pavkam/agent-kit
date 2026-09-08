// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns exact ordered selection evidence for atomic queue revalidation and commitment.</summary>
public sealed record InputPromotionPlan: InputPromotionPlanningResult
{
    /// <summary>Initializes a plan.</summary><param name="snapshot">The nonnull exact selection and ownership evidence.</param>
    public InputPromotionPlan(InputPromotionSnapshot snapshot) { ArgumentNullException.ThrowIfNull(snapshot); Snapshot = snapshot; }
    /// <summary>Gets selection snapshot.</summary><value>The exact immutable plan.</value>
    public InputPromotionSnapshot Snapshot { get; }
}
