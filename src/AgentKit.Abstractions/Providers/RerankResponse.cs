// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The terminal rerank response aggregate.</summary>
public sealed record RerankResponse
{
    /// <summary>Initializes a rerank response.</summary>
    /// <param name="results">The ordered rerank results.</param>
    /// <param name="usage">Usage evidence for the response.</param>
    /// <param name="providerRequestId">The provider request identifier, when available.</param>
    /// <param name="extensions">Provider-specific response metadata.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="results"/> is invalid.</exception>
    public RerankResponse(
        ImmutableArray<RerankResult> results,
        SemanticOperationUsage usage,
        ProviderRequestId? providerRequestId,
        ExtensionData extensions)
    {
        ArgumentException.ThrowIfDefault(results);
        ArgumentException.ThrowIfContainsNull(results);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(extensions);
        Results = results;
        Usage = usage;
        ProviderRequestId = providerRequestId;
        Extensions = extensions;
    }

    /// <summary>Gets the ordered rerank results.</summary>
    public ImmutableArray<RerankResult> Results { get; init; }

    /// <summary>Gets usage evidence.</summary>
    public SemanticOperationUsage Usage { get; init; }

    /// <summary>Gets the provider request identifier, when available.</summary>
    public ProviderRequestId? ProviderRequestId { get; init; }

    /// <summary>Gets provider-specific response metadata.</summary>
    public ExtensionData Extensions { get; init; }
}
