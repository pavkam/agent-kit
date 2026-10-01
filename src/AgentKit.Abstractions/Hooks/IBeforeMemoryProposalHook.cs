// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A hook that runs before a memory proposal is evaluated by memory policy.</summary>
/// <remarks>
/// Implementations run sequentially in the deterministic order of the captured hook catalog and may change only what
/// <see cref="BeforeMemoryProposalEventArgs"/> documents as writable. A hook cannot grant, widen, forge, or consume security authority; protected
/// operations it performs use the same authority as every other component. An exception, an invalid mutation, or a timeout
/// fails the memory operation: this point never isolates failure.
/// </remarks>
public interface IBeforeMemoryProposalHook
{
    /// <summary>Observes or narrows the event.</summary>
    /// <param name="args">The event arguments; only the documented writable members may change.</param>
    /// <param name="context">The identity and ordering evidence of this invocation.</param>
    /// <param name="cancellationToken">A token used to cancel the invocation; cancellation always propagates.</param>
    /// <returns>A task that completes when the hook has finished.</returns>
    public ValueTask InvokeAsync(
        BeforeMemoryProposalEventArgs args,
        HookInvocationContext context,
        CancellationToken cancellationToken = default);
}
