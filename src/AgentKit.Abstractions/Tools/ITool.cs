// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One invocable tool: its catalog-facing description and the invocation
/// behavior that runs once a call has already been resolved and
/// authorized.
/// </summary>
/// <remarks>
/// <para>
/// A tool never authorizes itself, never records its own audit trail, and
/// never decides whether its call was accepted; the tool runtime owns resolution,
/// authorization, and terminal recording. A tool
/// implementation is responsible only for parsing and validating its own
/// arguments and producing a result, and it must never crash the invoker on
/// malformed input — malformed arguments are reported through
/// <see cref="ToolCallOutcomeKind.Failed"/>, not a thrown exception, so the
/// invoker can produce a normal terminal result instead of treating every
/// bad argument as an unexpected failure.
/// </para>
/// <para>
/// Implementations must be safe to invoke concurrently for independent
/// requests; a tool has no per-call mutable state of its own; whatever
/// state exists (a file system, an in-memory store) is reached through an
/// injected dependency, never through fields the tool mutates across
/// calls.
/// </para>
/// </remarks>
public interface ITool
{
    /// <summary>Gets the catalog-facing description of this tool.</summary>
    public ToolDescriptor Descriptor { get; }

    /// <summary>Invokes this tool.</summary>
    /// <param name="request">The invocation request, already resolved and authorized.</param>
    /// <param name="cancellationToken">A token used to cancel the invocation.</param>
    /// <returns>A task producing the terminal outcome and result content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default);
}
