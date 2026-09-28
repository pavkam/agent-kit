// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares the distance function a vector index uses for compatibility checks and search.</summary>
public enum VectorDistanceMetric
{
    /// <summary>Cosine distance or similarity over normalized vectors.</summary>
    Cosine,

    /// <summary>Euclidean (L2) distance.</summary>
    Euclidean,

    /// <summary>Inner product similarity, typically for normalized embeddings.</summary>
    DotProduct,
}
