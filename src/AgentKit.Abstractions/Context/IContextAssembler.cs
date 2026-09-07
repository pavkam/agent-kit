// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Builds the bounded, provider-ready view for one model request from
/// already-loaded conversation history and instructions.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberately reduced stand-in for the fuller
/// <c>IContextAssembler</c> described by the context architecture, which
/// additionally orchestrates history loading through session contracts,
/// authorized retrieval, compaction, tool-catalog and output-contract
/// resolution, and budget allocation across many ordered contributors. This
/// reduced contract still owns the two structural responsibilities every
/// richer implementation must also perform: repairing history so only
/// complete, causally intact content ever reaches a provider, and
/// combining instructions, history, tools, and settings into one immutable
/// <see cref="LlmRequestContext"/>.
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
