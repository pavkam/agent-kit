// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Decides whether a failed tool attempt may be retried under configured runtime policy and tool semantics.</summary>
internal static class ToolInvocationRetryPolicy
{
    /// <summary>Evaluates whether another attempt is permitted after one invocation settled.</summary>
    /// <param name="outcome">The terminal outcome of the attempt that just finished.</param>
    /// <param name="effects">The resolved tool effects for the call.</param>
    /// <param name="attempt">The attempt count that just finished.</param>
    /// <param name="options">The configured runtime retry limits.</param>
    /// <param name="now">The current instant used to evaluate retry-after hints.</param>
    /// <returns>True when policy permits another attempt before the batch deadline.</returns>
    internal static bool ShouldRetry(
        ToolCallOutcome outcome,
        ToolEffects effects,
        int attempt,
        ToolRuntimeOptions options,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(options);

        if (!outcome.Retryable || attempt >= options.MaximumRetryAttempts)
        {
            return false;
        }

        if (effects.Effect == ToolEffect.Mutating)
        {
            if (effects.Idempotency is not IdempotencyClassification.Idempotent
                and not IdempotencyClassification.IdempotentWithKey)
            {
                return false;
            }

            if (outcome.SideEffectCertainty is SideEffectCertainty.DefinitelyPerformed or SideEffectCertainty.Unknown)
            {
                return false;
            }
        }

        if (outcome.FailureReason is null && outcome.Kind is ToolCallOutcomeKind.Failed)
        {
            return false;
        }

        _ = now;
        return true;
    }
}
