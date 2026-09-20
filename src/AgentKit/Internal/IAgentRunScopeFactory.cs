// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

using AgentKit;

/// <summary>Creates one run scope and compiles its plan without exposing the scope's service provider.</summary>
/// <remarks>
/// This is the only runtime type that owns <see cref="IServiceScopeFactory"/> for run activation. Callers receive
/// the plan and an opaque lifetime lease.
/// </remarks>
internal interface IAgentRunScopeFactory
{
    /// <summary>Creates a scope, compiles the plan, and returns both as a lease.</summary>
    /// <param name="definition">The resolved definition to activate.</param>
    /// <param name="request">The caller request the compiler validates.</param>
    /// <param name="cancellationToken">Cancels compilation. A cancelled call disposes the scope.</param>
    /// <returns>A lease whose disposal releases the scope.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The plan could not be compiled.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    internal ValueTask<AgentRunScopeLease> CreateAsync(
        ResolvedAgentDefinition definition,
        AgentRunRequest request,
        CancellationToken cancellationToken);
}
