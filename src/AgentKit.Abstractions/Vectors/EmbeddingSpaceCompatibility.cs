// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines when two embedding-space identities describe one comparable vector space.</summary>
public static class EmbeddingSpaceCompatibility
{
    extension(EmbeddingSpaceIdentity space)
    {
        /// <summary>Determines whether another identity describes the same comparable vector space.</summary>
        /// <param name="other">The identity to compare.</param>
        /// <returns>
        /// <see langword="true"/> only when the provider, upstream provider, API family, resolved model, deployment, dimensions,
        /// element type, normalization, truncation, endpoint, service surface, and model revision all match. A matching dimension
        /// count alone is never compatibility.
        /// </returns>
        /// <remarks>
        /// Per-response facts (request and response identifiers), the requested model alias, extension data, and
        /// <see cref="EmbeddingSpaceIdentity.Purpose"/> are deliberately ignored: a query embedding and a document embedding from
        /// the same model revision live in one space, and identifiers vary per call without changing the space.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public bool IsSameVectorSpaceAs(EmbeddingSpaceIdentity other)
        {
            ArgumentNullException.ThrowIfNull(other);
            ArgumentNullException.ThrowIfNull(space);
            return space.Provider.ProviderId == other.Provider.ProviderId
                && space.Provider.UpstreamProviderId == other.Provider.UpstreamProviderId
                && space.Provider.ApiFamily == other.Provider.ApiFamily
                && space.Provider.ResolvedModelId == other.Provider.ResolvedModelId
                && space.Provider.DeploymentId == other.Provider.DeploymentId
                && space.Dimensions == other.Dimensions
                && space.ElementType == other.ElementType
                && space.Normalization == other.Normalization
                && space.Truncation == other.Truncation
                && space.EndpointId == other.EndpointId
                && space.ServiceSurface == other.ServiceSurface
                && space.ModelRevision == other.ModelRevision;
        }
    }
}
