// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Lets an invoker state that it enforces a call's declared idempotency mechanism, so a possibly-started attempt may be retried.</summary>
/// <remarks>
/// <para>
/// A descriptor's idempotency declaration alone never proves replay safety. The executor retries a mutating call whose
/// previous attempt may have started only when the invoker implements this interface and confirms enforcement for the
/// exact context, including <see cref="ToolInvocationContext.ExternalIdempotencyKey"/> for keyed idempotency. An invoker
/// that does not implement it is never retried after a possibly-started failure.
/// </para>
/// <para>The method must be side-effect free and fast; it is consulted before every such retry.</para>
/// </remarks>
public interface IIdempotencyEnforcingToolInvoker: IToolInvoker
{
    /// <summary>Determines whether replaying <paramref name="context"/> is made safe by this invoker and its effecting host.</summary>
    /// <param name="context">The nonnull context the retry would reuse, including its captured idempotency key.</param>
    /// <returns><see langword="true"/> only when the declared idempotency mechanism will be enforced for this exact context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public bool EnforcesIdempotency(ToolInvocationContext context);
}
