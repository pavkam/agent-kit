// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Contributes bounded, trust-tagged context candidates for one assembly evaluation.</summary>
/// <remarks>
/// Implementations return proposals only; they do not mutate durable history, issue grants, or invoke providers.
/// </remarks>
public interface IContextContributor
{
    /// <summary>Produces zero or more candidates and safe diagnostics for one evaluation.</summary>
    /// <param name="request">The immutable contributor evidence.</param>
    /// <param name="cancellationToken">Cancels the contribution before it returns.</param>
    /// <returns>A bounded contribution that the assembler merges deterministically.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask<ContextContribution> ContributeAsync(
        ContextContributionRequest request,
        CancellationToken cancellationToken = default);
}
