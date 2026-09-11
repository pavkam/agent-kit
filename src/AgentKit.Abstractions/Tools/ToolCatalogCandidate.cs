// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one toolset's exact descriptor, source publication, and execution-policy contribution.</summary>
/// <remarks>This immutable evidence contains no invoker or authority. Aliases are read from the retained toolset; a policy cannot add an alias to a candidate.</remarks>
public sealed record ToolCatalogCandidate
{
    /// <summary>Validates a contribution against its complete retained publications.</summary>
    /// <param name="toolset">The nonnull originating toolset publication.</param>
    /// <param name="source">The nonnull source publication selected by that toolset.</param>
    /// <param name="identity">The nondefault exact identity present in the source.</param>
    /// <exception cref="ArgumentNullException">A publication is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="identity"/> is default.</exception>
    /// <exception cref="ArgumentException">The toolset does not select the source, or the identity is absent from it.</exception>
    public ToolCatalogCandidate(ToolsetPublication toolset, ToolProviderSnapshot source, ToolIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(toolset);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfEqual(identity, default);
        ArgumentException.ThrowIfNotEqual(toolset.Sources.Any(selection => selection.SourceId == source.SourceId), true, nameof(source));
        var tool = source.Tools.SingleOrDefault(descriptor => descriptor.Id == identity.Id && descriptor.Version == identity.Version);
        ArgumentException.ThrowIfNotEqual(tool is not null, true, nameof(identity));
        Toolset = toolset;
        Source = source;
        Tool = tool!;
    }

    /// <summary>Gets the publication establishing membership, aliases, and execution policy.</summary>
    /// <value>The immutable originating toolset, including its exact version.</value>
    public ToolsetPublication Toolset { get; }

    /// <summary>Gets the exact source publication supplying the descriptor.</summary>
    /// <value>The retained source metadata; its lifetime owner is held separately by the catalog.</value>
    public ToolProviderSnapshot Source { get; }

    /// <summary>Gets the exact descriptor found in the source publication.</summary>
    /// <value>The immutable source-authored descriptor, without policy modifications.</value>
    public ToolDescriptor Tool { get; }

    /// <summary>Gets the exact canonical identity of this contribution.</summary>
    /// <value>The descriptor's nondefault ID and version.</value>
    public ToolIdentity Identity => new(Tool.Id, Tool.Version);
}
