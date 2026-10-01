// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Decides whether one memory proposal may become durable state.</summary>
/// <remarks>
/// A policy validates provenance, scope, sensitivity, contradiction, and classification. It does not write, authorize a
/// protected effect, or mint grants. Implementations are thread-safe and deterministic for identical inputs; a policy that
/// fails is treated as a denial.
/// </remarks>
public interface IMemoryPolicy
{
    /// <summary>Evaluates one proposal.</summary>
    /// <param name="proposal">The proposal, carrying its operation context.</param>
    /// <param name="context">The profile and instant.</param>
    /// <param name="cancellationToken">Cancels evaluation.</param>
    /// <returns>An allow or a typed denial.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="proposal"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default);
}
