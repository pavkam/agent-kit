// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Enforces the item, byte, and token bounds of one retrieval over ranked candidates.</summary>
/// <remarks>A policy only removes candidates; it never reorders, rewrites, or truncates them, so provenance and text stay exact. Implementations are deterministic and thread-safe.</remarks>
public interface IRetrievalBudgetPolicy
{
    /// <summary>Selects the ranked candidates that fit the budget.</summary>
    /// <param name="request">The budget and ranked candidates.</param>
    /// <param name="cancellationToken">Cancels the decision.</param>
    /// <returns>The selected subsequence in rank order and the number omitted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<RetrievalBudgetDecision> SelectAsync(RetrievalBudgetRequest request, CancellationToken cancellationToken = default);
}
