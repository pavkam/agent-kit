// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed family of outcomes from planning and attempting one atomic input promotion.</summary>
/// <remarks>The result distinguishes committed promotion, stale evidence, and pre-mutation rejection. It never implies that no eligible input exists merely because promotion did not commit.</remarks>
public abstract record InputPromotionResult
{
    /// <summary>Initializes one canonical atomic-promotion outcome.</summary>
    /// <remarks>External assemblies cannot extend this hierarchy, preserving exhaustive handling of committed, stale, and rejected outcomes.</remarks>
    private protected InputPromotionResult() { }
}
