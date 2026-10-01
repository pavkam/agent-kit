// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Builds the bounded, provider-ready view for one model request from
/// already-loaded conversation history and instructions.
/// </summary>
/// <remarks>
/// <para>
/// An implementation owns two structural responsibilities: repairing history
/// so only complete, causally intact content ever reaches a provider, and
/// combining instructions, history, tools, settings, and the output contract
/// into one immutable <see cref="LlmRequestContext"/>. The request carries the
/// already-loaded, pinned history inside its <see cref="ContextAssemblyEvidence"/>;
/// the first-party assembler additionally runs its keyed, ordered contributors
/// and allocates their candidates against a budget, and the loop owns history
/// loading and compaction.
/// </para>
/// <para>
/// Assembling context never mutates durable session history; it only reads
/// from the request it is given.
/// </para>
/// </remarks>
public interface IContextAssembler
{
    /// <summary>Assembles one provider-ready request.</summary>
    /// <param name="request">The assembly request.</param>
    /// <param name="cancellationToken">A token used to cancel assembly.</param>
    /// <returns>A task producing the closed assembly outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public Task<ContextAssemblyResult> AssembleAsync(
        ContextAssemblyRequest request, CancellationToken cancellationToken = default);
}
