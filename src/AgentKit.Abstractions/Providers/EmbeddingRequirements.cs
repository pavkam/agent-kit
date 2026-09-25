// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Portable behaviors one embedding request needs before a model is chosen.</summary>
public sealed record EmbeddingRequirements
{
    /// <summary>Gets a requirements value that accepts any configured embedding model.</summary>
    public static EmbeddingRequirements None { get; } = new();

    /// <summary>Gets whether the request needs batched input support.</summary>
    public bool RequiresBatchInput { get; init; }

    /// <summary>Gets whether the request needs reduced-dimensionality support.</summary>
    public bool RequiresDimensions { get; init; }

    /// <summary>Gets whether the request needs purpose-specific transforms.</summary>
    public bool RequiresPurpose { get; init; }

    /// <summary>Gets whether the request needs encoding selection support.</summary>
    public bool RequiresEncodingSelection { get; init; }

    /// <summary>Gets whether the request needs explicit truncation control.</summary>
    public bool RequiresTruncationControl { get; init; }
}
