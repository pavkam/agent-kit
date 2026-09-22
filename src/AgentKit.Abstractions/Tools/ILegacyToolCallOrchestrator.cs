// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Orchestrates one complete legacy tool call: resolving <see cref="LegacyToolCallRequest.Tool"/>
/// against an <see cref="IToolCatalog"/>, authorizing it through an
/// <see cref="IToolAuthorizer"/>, invoking the resolved <see cref="ITool"/>,
/// and translating every outcome into one <see cref="ResolvedToolInvocation"/>.
/// </summary>
/// <remarks>
/// This contract is the interim reduced orchestrator retained while the loop consumes
/// <see cref="IToolExecutor"/> and per-tool invokers implement spec-shaped <see cref="IToolInvoker"/>.
/// It will be deleted when the spec-shaped tool executor replaces
/// <see cref="IToolExecutor"/> implementations that adapt this orchestrator (workstream 4 chunk C10a).
/// </remarks>
public interface ILegacyToolCallOrchestrator
{
    /// <summary>Resolves, authorizes, and invokes one legacy tool call.</summary>
    /// <param name="request">The call request, whose <see cref="LegacyToolCallRequest.Tool"/> may be unresolved.</param>
    /// <param name="cancellationToken">A token used to cancel the call.</param>
    /// <returns>A task producing the resolved-or-not tool reference alongside the terminal outcome and result content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled before the call settles.
    /// </exception>
    public Task<ResolvedToolInvocation> InvokeAsync(LegacyToolCallRequest request, CancellationToken cancellationToken = default);
}
