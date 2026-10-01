// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Runs every policy registered for a profile's policy key and combines their decisions.</summary>
/// <remarks>The combined decision is monotonic toward refusal: any denial, any policy failure, or the absence of an explicit allow (under the default acceptance mode) refuses the proposal.</remarks>
public interface IMemoryPolicyDispatcher
{
    /// <summary>Evaluates one proposal against the profile's policies.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <param name="context">The profile and instant.</param>
    /// <param name="cancellationToken">Cancels evaluation.</param>
    /// <returns>The combined decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="proposal"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default);
}
