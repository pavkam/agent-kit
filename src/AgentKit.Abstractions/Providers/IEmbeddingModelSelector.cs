// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one compatible embedding model from a catalog snapshot.</summary>
public interface IEmbeddingModelSelector
{
    /// <summary>Selects one embedding model for <paramref name="request"/>.</summary>
    /// <param name="request">The selection request.</param>
    /// <param name="cancellationToken">A token used to cancel selection.</param>
    /// <returns>The selection outcome.</returns>
    public ValueTask<EmbeddingSelectionResult> SelectAsync(
        EmbeddingSelectionRequest request,
        CancellationToken cancellationToken = default);
}
