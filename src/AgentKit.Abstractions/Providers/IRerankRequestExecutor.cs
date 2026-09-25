// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Executes one captured reranker selection with bounded same-model retries.</summary>
public interface IRerankRequestExecutor
{
    /// <summary>Executes <paramref name="request"/>.</summary>
    /// <param name="request">The captured execution request.</param>
    /// <param name="cancellationToken">A token used to cancel execution.</param>
    /// <returns>The terminal execution outcome.</returns>
    public Task<RerankExecutionResult> ExecuteAsync(
        RerankExecutionRequest request,
        CancellationToken cancellationToken = default);
}
