// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the exact reranker selector, executor, and ordered alias policy a memory profile captured.</summary>
/// <remarks>Reranking is enabled only by a complete triple. There is no engine-global default semantic operation.</remarks>
public sealed record RerankerRuntimeReference
{
    /// <summary>Initializes a validated reference.</summary>
    /// <param name="selectorKey">The key of the reranker selector.</param>
    /// <param name="executorKey">The key of the rerank request executor.</param>
    /// <param name="policy">The ordered alias policy with at least one candidate.</param>
    /// <exception cref="ArgumentException">A key is blank or the policy has no candidates.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public RerankerRuntimeReference(
        ComponentKey<IRerankerSelector> selectorKey,
        ComponentKey<IRerankRequestExecutor> executorKey,
        RerankerSelectionPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectorKey.Value, nameof(selectorKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(executorKey.Value, nameof(executorKey));
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfDefaultOrEmpty(policy.Candidates, nameof(policy));
        SelectorKey = selectorKey;
        ExecutorKey = executorKey;
        Policy = policy;
    }

    /// <summary>Gets the key of the reranker selector.</summary>
    public ComponentKey<IRerankerSelector> SelectorKey { get; }

    /// <summary>Gets the key of the rerank request executor.</summary>
    public ComponentKey<IRerankRequestExecutor> ExecutorKey { get; }

    /// <summary>Gets the ordered alias policy.</summary>
    public RerankerSelectionPolicy Policy { get; }
}
