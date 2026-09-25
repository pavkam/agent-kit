// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>One configured reranker implementation.</summary>
public interface IReranker
{
    /// <summary>Gets the application-facing selection key this instance serves.</summary>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "'Alias' is the established AgentKit provider-catalog term.")]
    public RerankerAlias Alias { get; }

    /// <summary>Performs one rerank attempt.</summary>
    /// <param name="request">The rerank request.</param>
    /// <param name="cancellationToken">A token used to cancel the attempt.</param>
    /// <returns>The terminal attempt outcome.</returns>
    public Task<RerankModelResult> RerankAsync(
        RerankModelRequest request,
        CancellationToken cancellationToken = default);
}
