// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names a loop boundary at which captured input may safely be promoted.</summary>
public enum PromotionBoundary
{
    /// <summary>Occurs before the run's first model request.</summary>
    BeforeFirstModelRequest,
    /// <summary>Occurs after an assistant message and every accepted tool result for its turn are committed.</summary>
    AfterTurnCommitted,
    /// <summary>Occurs after a continuation checkpoint explicitly declared safe by the loop.</summary>
    AfterContinuationCheckpoint,
    /// <summary>Occurs when the run would otherwise finish without more work.</summary>
    OtherwiseIdle,
}
