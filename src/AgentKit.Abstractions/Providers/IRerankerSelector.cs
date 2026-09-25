// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one compatible reranker from a catalog snapshot.</summary>
public interface IRerankerSelector
{
    /// <summary>Selects one reranker for <paramref name="request"/>.</summary>
    /// <param name="request">The selection request.</param>
    /// <param name="cancellationToken">A token used to cancel selection.</param>
    /// <returns>The selection outcome.</returns>
    public ValueTask<RerankerSelectionResult> SelectAsync(
        RerankerSelectionRequest request,
        CancellationToken cancellationToken = default);
}
