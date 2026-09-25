// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>Translates provider-neutral rerank requests into Cohere v2 rerank bodies.</summary>
public interface ICohereRerankRequestTranslator
{
    /// <summary>Translates one rerank attempt into a Cohere v2 rerank JSON body.</summary>
    /// <param name="request">The rerank attempt request.</param>
    /// <param name="descriptor">The reranker descriptor served by the adapter.</param>
    /// <returns>The request body object.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="NotSupportedException">The request shape is not supported by Cohere v2 rerank.</exception>
    public JsonObject Translate(RerankModelRequest request, RerankerDescriptor descriptor);
}
