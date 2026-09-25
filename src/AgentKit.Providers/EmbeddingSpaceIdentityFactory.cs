// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Builds <see cref="EmbeddingSpaceIdentity"/> values with shared stamping fields.</summary>
public static class EmbeddingSpaceIdentityFactory
{
    /// <summary>
    /// Creates one embedding space identity with optional endpoint, surface, normalization, truncation, and revision
    /// metadata stamped from the parse context.
    /// </summary>
    /// <param name="provider">The provider response identity for the vector.</param>
    /// <param name="dimensions">The vector dimension count.</param>
    /// <param name="elementType">The element representation.</param>
    /// <param name="purpose">The portable embedding purpose.</param>
    /// <param name="extensions">Provider-specific space data.</param>
    /// <param name="context">The parse context supplying optional stamping metadata.</param>
    /// <returns>A fully stamped space identity.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimensions"/> is less than one.</exception>
    public static EmbeddingSpaceIdentity Create(
        ProviderResponseIdentity provider,
        int dimensions,
        EmbeddingElementType elementType,
        EmbeddingPurpose purpose,
        ExtensionData extensions,
        EmbeddingResponseParseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var space = new EmbeddingSpaceIdentity(provider, dimensions, elementType, purpose, extensions)
        {
            Normalization = context.Normalization,
            Truncation = context.Truncation,
            ModelRevision = context.ModelRevision,
        };

        if (context.EndpointId is { Value: not null })
        {
            space = space with { EndpointId = context.EndpointId.Value };
        }

        if (context.ServiceSurface is { Value: not null })
        {
            space = space with { ServiceSurface = context.ServiceSurface.Value };
        }

        return space;
    }
}
