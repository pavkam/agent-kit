// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Reports that catalog collision policy rejected the discovered contribution graph.</summary>
/// <remarks>The coordinator already released every discovered source before returning this outcome.</remarks>
internal sealed record ToolCatalogCoordinatorMergeRejected: ToolCatalogCoordinatorResult
{
    /// <summary>Retains the complete rejected contribution and collision evidence.</summary>
    /// <param name="context">The nonnull evidence policy evaluated before rejecting.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    internal ToolCatalogCoordinatorMergeRejected(ToolCatalogMergeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    /// <summary>Gets the complete rejected contribution and collision evidence.</summary>
    /// <value>A nonnull context; no partial catalog was ever constructed from it.</value>
    internal ToolCatalogMergeContext Context { get; }
}
