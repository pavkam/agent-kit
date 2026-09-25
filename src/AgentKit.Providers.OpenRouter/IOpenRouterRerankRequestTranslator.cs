// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

/// <summary>Translates provider-neutral rerank requests into OpenRouter rerank bodies.</summary>
public interface IOpenRouterRerankRequestTranslator
{
    /// <summary>Translates one rerank attempt into an OpenRouter rerank JSON body.</summary>
    /// <param name="request">The rerank attempt request.</param>
    /// <param name="descriptor">The reranker descriptor served by the adapter.</param>
    /// <returns>The request body object.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="NotSupportedException">The request shape is not supported by OpenRouter rerank.</exception>
    public JsonObject Translate(RerankModelRequest request, RerankerDescriptor descriptor);
}
