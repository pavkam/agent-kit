// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares how a response mixing output-tool and function-tool calls picks
/// its winning output candidate.
/// </summary>
/// <remarks>
/// When a terminal response mixes synthetic output-tool calls with application function-tool calls, the agent loop
/// applies this strategy before invoking avoidable side effects.
/// </remarks>
public enum OutputEndStrategy
{
    /// <summary>Validate output candidates in source order until one succeeds; run function calls on schedule.</summary>
    Graceful,

    /// <summary>Stop at the first valid output candidate; skip function calls not already required.</summary>
    Early,

    /// <summary>Validate every output candidate and run every function call; the winner is the first valid output by source order.</summary>
    Exhaustive
}
