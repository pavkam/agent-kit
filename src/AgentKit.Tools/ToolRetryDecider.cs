// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Decides whether a failed tool attempt is eligible for another attempt under the planned <see cref="ToolRetryPolicy"/> and the tool's declared semantics.</summary>
/// <remarks>
/// <para>
/// A retry requires both a retryable failure and safe execution semantics. A read-only call, or a mutating call the
/// invoker reports it definitely did not perform, may be retried under ordinary policy. A mutating call whose previous
/// attempt may have started is retried only when its descriptor declares <see cref="IdempotencyClassification.Idempotent"/>
/// or <see cref="IdempotencyClassification.IdempotentWithKey"/> (with the captured external key) <em>and</em> the invoker
/// implements <see cref="IIdempotencyEnforcingToolInvoker"/> and confirms enforcement for the exact context. A declaration
/// alone never proves replay safety. Cancellation and interruption are never retried.
/// </para>
/// <para>Reasons are a closed vocabulary shared with the retry metric: they never carry content.</para>
/// </remarks>
internal static class ToolRetryDecider
{
    /// <summary>Gets the reason string meaning a retry was scheduled.</summary>
    internal const string Scheduled = "scheduled";

    /// <summary>Returns why the failed attempt is not eligible for retry, or null when it is eligible.</summary>
    /// <param name="invocation">The raw evidence of the attempt that just settled.</param>
    /// <param name="context">The exact context the attempt ran with.</param>
    /// <param name="invoker">The invoker that ran the attempt.</param>
    /// <param name="policy">The planned retry pacing and budget.</param>
    /// <param name="attempt">The positive attempt that just settled, counting the first attempt as one.</param>
    /// <returns>A closed bounded reason, or null when another attempt is permitted subject to the batch deadline.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempt"/> is not positive.</exception>
    internal static string? Decline(
        ToolInvocationResult invocation,
        ToolInvocationContext context,
        IToolInvoker invoker,
        ToolRetryPolicy policy,
        int attempt)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempt);
        var outcome = invocation.Outcome;
        if (outcome.Kind is ToolCallOutcomeKind.Success)
        {
            return "succeeded";
        }

        if (outcome.Kind is ToolCallOutcomeKind.Cancelled)
        {
            return "cancelled";
        }

        if (!outcome.Retryable)
        {
            return "not_retryable";
        }

        if (attempt >= policy.MaximumAttempts)
        {
            return "exhausted";
        }

        var effects = context.Tool.Effects;
        if (effects.Effect is ToolEffect.ReadOnly || outcome.SideEffectCertainty is SideEffectCertainty.DefinitelyNotPerformed)
        {
            return null;
        }

        var declaredSafe = effects.Idempotency is IdempotencyClassification.Idempotent
            || (effects.Idempotency is IdempotencyClassification.IdempotentWithKey && context.ExternalIdempotencyKey.HasValue);
        return declaredSafe && invoker is IIdempotencyEnforcingToolInvoker enforcing && enforcing.EnforcesIdempotency(context)
            ? null
            : "unsafe";
    }
}
