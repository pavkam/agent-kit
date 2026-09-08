// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed result of deterministic, side-effect-free input-promotion planning.</summary>
/// <remarks>A planning result either supplies a complete ordered plan or explains why no such plan can be formed from the captured context. It does not mutate queue or session state.</remarks>
public abstract record InputPromotionPlanningResult
{
    /// <summary>Initializes one canonical side-effect-free planning outcome.</summary>
    /// <remarks>External assemblies cannot extend this hierarchy, so policy consumers can exhaustively handle a plan or typed complete-plan rejection.</remarks>
    private protected InputPromotionPlanningResult() { }
}
