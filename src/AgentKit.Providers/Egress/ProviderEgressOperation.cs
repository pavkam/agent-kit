// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

/// <summary>Names the independent provider operation a provider-egress attempt performs.</summary>
/// <remarks>
/// Conversation, embedding, and reranking are separate provider operations with separate bindings, so each
/// binds a distinct provider-egress grant. The value is bounded and is safe as a metric and trace dimension.
/// </remarks>
public enum ProviderEgressOperation
{
    /// <summary>One conversational model request, buffered or streamed.</summary>
    Conversation = 0,

    /// <summary>One embedding request.</summary>
    Embedding = 1,

    /// <summary>One reranking request.</summary>
    Reranking = 2,
}
