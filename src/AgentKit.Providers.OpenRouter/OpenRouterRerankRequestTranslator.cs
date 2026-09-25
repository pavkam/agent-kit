// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

/// <summary>The default <see cref="IOpenRouterRerankRequestTranslator"/> for OpenRouter rerank.</summary>
public sealed class OpenRouterRerankRequestTranslator: IOpenRouterRerankRequestTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(RerankModelRequest request, RerankerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(descriptor);

        var rerank = request.Request;
        var documents = new JsonArray();
        foreach (var document in rerank.Documents)
        {
            documents.Add(document.Text);
        }

        var body = new JsonObject
        {
            ["model"] = descriptor.ModelId.Value,
            ["query"] = rerank.Query,
            ["documents"] = documents,
        };

        if (rerank.TopCount is { } topCount)
        {
            body["top_n"] = topCount;
        }

        ProviderJson.ApplyExtensions(body, rerank.Options.Extensions);
        return body;
    }
}
