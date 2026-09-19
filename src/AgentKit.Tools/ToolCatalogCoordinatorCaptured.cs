// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Reports a complete catalog capture after discovery, merge, and schema/capability preflight all succeeded.</summary>
internal sealed record ToolCatalogCoordinatorCaptured: ToolCatalogCoordinatorResult
{
    /// <summary>Transfers the new sole owner of the discovered source graph to its caller.</summary>
    /// <param name="catalog">The nonnull already validated catalog capture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
    internal ToolCatalogCoordinatorCaptured(ToolCatalogCapture catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
    }

    /// <summary>Gets the sole owner of the discovered source graph.</summary>
    /// <value>A nonnull catalog capture; the caller must dispose it to release the underlying sources.</value>
    internal ToolCatalogCapture Catalog { get; }
}
