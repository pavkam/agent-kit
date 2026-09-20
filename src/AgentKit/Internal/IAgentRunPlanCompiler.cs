// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

using AgentKit;

/// <summary>Compiles one immutable <see cref="AgentRunPlan"/> inside an already created run scope.</summary>
/// <remarks>
/// The compiler validates contract and key selection, the run-profile publication, and the catalog version
/// before the runtime mutates a session or calls a provider. It is not a public extension point.
/// </remarks>
internal interface IAgentRunPlanCompiler
{
    /// <summary>Compiles the plan for one already resolved definition and caller request.</summary>
    /// <param name="definition">The definition the runtime has already resolved from the catalog.</param>
    /// <param name="request">The caller request, including the session that admission will use.</param>
    /// <param name="cancellationToken">Cancels compilation. Cancellation leaves no session mutation from this call.</param>
    /// <returns>A compiled plan, or diagnostics when the scope cannot satisfy the definition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    internal ValueTask<AgentRunPlanCompilationResult> CompileAsync(
        ResolvedAgentDefinition definition,
        AgentRunRequest request,
        CancellationToken cancellationToken);
}
