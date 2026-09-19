// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Orchestrates one complete tool call: resolving <see cref="LegacyToolCallRequest.Tool"/>
/// against an <see cref="IToolCatalog"/>, authorizing it through an
/// <see cref="IToolAuthorizer"/>, invoking the resolved <see cref="ITool"/>,
/// and translating every outcome — including an unknown tool, a denial, or
/// an unexpected exception — into one <see cref="ResolvedToolInvocation"/>.
/// </summary>
/// <remarks>
/// An invoker composes exactly one <see cref="IToolCatalog"/> and one
/// <see cref="IToolAuthorizer"/>; it never re-implements catalog resolution
/// or authorization policy inline. It never lets a tool's thrown exception
/// escape as a fault: every reachable failure — unknown tool, denied
/// authorization, or an unhandled exception from
/// <see cref="ITool.InvokeAsync"/> — becomes an ordinary
/// <see cref="ToolInvocationResult"/> whose <see cref="ToolCallOutcome.Kind"/>
/// classifies which of those happened, so a caller (typically an agent
/// loop) always receives exactly one terminal result per call regardless of
/// how it failed. An <see cref="OperationCanceledException"/> propagates only
/// when the caller-provided cancellation token is signaled; an implementation
/// that throws one without caller cancellation is normalized as a tool failure.
/// </remarks>
public interface IToolInvoker
{
    /// <summary>Resolves, authorizes, and invokes one tool call.</summary>
    /// <param name="request">The call request, whose <see cref="LegacyToolCallRequest.Tool"/> may be unresolved.</param>
    /// <param name="cancellationToken">A token used to cancel the call.</param>
    /// <returns>A task producing the resolved-or-not tool reference alongside the terminal outcome and result content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled before the call settles.
    /// </exception>
    public Task<ResolvedToolInvocation> InvokeAsync(LegacyToolCallRequest request, CancellationToken cancellationToken = default);
}
