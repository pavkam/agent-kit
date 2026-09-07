// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares how a response mixing output-tool and function-tool calls picks
/// its winning output candidate.
/// </summary>
/// <remarks>
/// This value is carried on every <see cref="OutputDefinition"/> for
/// forward compatibility with the full tool-scheduling architecture, but is
/// not yet behaviorally applied by the first-party <c>AgentKit.Output</c>
/// processor: that processor always evaluates the single terminal
/// <see cref="ModelResponse"/> it is given rather than scheduling and
/// racing multiple output-tool candidates, since multi-candidate scheduling
/// depends on the not-yet-implemented application tool-call scheduler.
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
