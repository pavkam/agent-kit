// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns exact ordered selection evidence for a later atomic queue revalidation and promotion attempt.</summary>
/// <remarks>A plan is side-effect-free and remains a proposal until the session owner accepts its snapshot in one promotion transition.</remarks>
public sealed record InputPromotionPlan: InputPromotionPlanningResult
{
    /// <summary>Initializes a side-effect-free promotion plan.</summary>
    /// <param name="snapshot">The non-null immutable selection, ownership, cutoff, and target-turn evidence to revalidate before promotion.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    public InputPromotionPlan(InputPromotionSnapshot snapshot) { ArgumentNullException.ThrowIfNull(snapshot); Snapshot = snapshot; }
    /// <summary>Gets the exact immutable promotion snapshot proposed by planning.</summary>
    /// <value>A non-null plan snapshot; reading it neither reserves queue capacity nor consumes an admission.</value>
    public InputPromotionSnapshot Snapshot { get; }
}
