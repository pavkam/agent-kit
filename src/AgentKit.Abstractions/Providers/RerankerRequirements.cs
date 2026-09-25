// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Portable behaviors one rerank request needs before a reranker is chosen.</summary>
public sealed record RerankerRequirements
{
    /// <summary>Gets a requirements value that accepts any configured reranker.</summary>
    public static RerankerRequirements None { get; } = new();

    /// <summary>Gets whether the request needs top-count support.</summary>
    public bool RequiresTopCount { get; init; }
}
