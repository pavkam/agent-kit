// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the exact embedding selector, executor, and ordered alias policy a memory profile captured.</summary>
/// <remarks>Embedding is enabled only by a complete triple. There is no engine-global default semantic operation: the runtime resolves exactly these keyed components, never whatever is registered first.</remarks>
public sealed record EmbeddingRuntimeReference
{
    /// <summary>Initializes a validated reference.</summary>
    /// <param name="selectorKey">The key of the embedding model selector.</param>
    /// <param name="executorKey">The key of the embedding request executor.</param>
    /// <param name="policy">The ordered alias policy with at least one candidate.</param>
    /// <exception cref="ArgumentException">A key is blank or the policy has no candidates.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public EmbeddingRuntimeReference(
        ComponentKey<IEmbeddingModelSelector> selectorKey,
        ComponentKey<IEmbeddingRequestExecutor> executorKey,
        EmbeddingSelectionPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectorKey.Value, nameof(selectorKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(executorKey.Value, nameof(executorKey));
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfDefaultOrEmpty(policy.Candidates, nameof(policy));
        SelectorKey = selectorKey;
        ExecutorKey = executorKey;
        Policy = policy;
    }

    /// <summary>Gets the key of the embedding model selector.</summary>
    public ComponentKey<IEmbeddingModelSelector> SelectorKey { get; }

    /// <summary>Gets the key of the embedding request executor.</summary>
    public ComponentKey<IEmbeddingRequestExecutor> ExecutorKey { get; }

    /// <summary>Gets the ordered alias policy.</summary>
    public EmbeddingSelectionPolicy Policy { get; }
}
