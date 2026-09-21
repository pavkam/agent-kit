// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable evidence one tool-snapshot resolution pass evaluates.</summary>
public sealed record ToolSnapshotRequest
{
    /// <summary>Initializes one tool-snapshot request.</summary>
    /// <param name="catalog">An optional captured catalog snapshot; when present its descriptors are projected.</param>
    /// <param name="fallbackTools">Tools to use when no catalog snapshot is supplied.</param>
    /// <exception cref="ArgumentException"><paramref name="fallbackTools"/> is a default array or contains null.</exception>
    public ToolSnapshotRequest(ToolCatalogSnapshot? catalog, ImmutableArray<LlmToolDefinition> fallbackTools)
    {
        ArgumentException.ThrowIfContainsNull(fallbackTools);
        Catalog = catalog;
        FallbackTools = fallbackTools;
    }

    /// <summary>Gets an optional captured catalog snapshot.</summary>
    public ToolCatalogSnapshot? Catalog { get; }

    /// <summary>Gets tools to use when <see cref="Catalog"/> is absent.</summary>
    public ImmutableArray<LlmToolDefinition> FallbackTools { get; }
}
