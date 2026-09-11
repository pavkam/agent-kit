// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Marks an explicitly registered typed source key for composition-time binding capture.</summary>
/// <remarks>The marker contains no container, factory, provider, or live run state.</remarks>
internal sealed record ToolProviderRegistration
{
    /// <summary>Captures an explicit source key without activating its provider.</summary>
    /// <param name="sourceId">The nondefault exact typed registration key.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/> is default.</exception>
    internal ToolProviderRegistration(ToolSourceId sourceId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
        SourceId = sourceId;
    }
    /// <summary>Gets the source key to resolve once when the registration catalog is constructed.</summary>
    /// <value>A nondefault ordinal source identity, never an unkeyed fallback.</value>
    internal ToolSourceId SourceId { get; }
}
