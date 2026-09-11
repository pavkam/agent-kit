// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains one source's ordered, immutable tool publication independently of its invoker acquisitions.</summary>
/// <remarks>
/// Exact identities are unique within one source publication. Names may repeat across distinct identities;
/// catalog merging and explicit alias assignments decide exposure. This value carries no live invokers,
/// owns no provider lifetime, and does not prove schema support or grant authority.
/// </remarks>
public sealed record ToolProviderSnapshot
{
    /// <summary>Captures a locally coherent source publication without reading live provider metadata.</summary>
    /// <param name="sourceId">The nondefault stable source that owns every supplied descriptor.</param>
    /// <param name="sourceVersion">The nondefault, source-defined publication version, compared ordinally.</param>
    /// <param name="tools">Initialized, ordered descriptors with unique exact tool identities and the same source identity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/> or <paramref name="sourceVersion"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="tools"/> is uninitialized, contains null, repeats an exact identity, or contains a descriptor from another source.</exception>
    public ToolProviderSnapshot(ToolSourceId sourceId, ToolSourceVersion sourceVersion, ImmutableArray<ToolDescriptor> tools)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sourceVersion, default);
        ArgumentException.ThrowIfContainsNull(tools);
        var identities = new HashSet<ToolIdentity>();
        foreach (var tool in tools)
        {
            ArgumentException.ThrowIfNotEqual(tool.SourceId, sourceId, nameof(tools));
            ArgumentException.ThrowIfNotEqual(identities.Add(new ToolIdentity(tool.Id, tool.Version)), true, nameof(tools));
        }

        SourceId = sourceId;
        SourceVersion = sourceVersion;
        Tools = tools;
    }

    /// <summary>Gets the source that owns this publication and each descriptor in it.</summary>
    /// <value>The nondefault source identity captured at construction; it is not inferred from a tool name or CLR type.</value>
    public ToolSourceId SourceId { get; }

    /// <summary>Gets the source-defined immutable publication version.</summary>
    /// <value>Nondefault ordinal version text; it carries no cross-source ordering or lease guarantee.</value>
    public ToolSourceVersion SourceVersion { get; }

    /// <summary>Gets the ordered descriptor snapshot, including a valid empty publication.</summary>
    /// <value>An initialized immutable sequence of same-source descriptors with unique exact identities.</value>
    public ImmutableArray<ToolDescriptor> Tools { get; }

    /// <summary>Compares the publication identity and complete ordered descriptor content.</summary>
    /// <param name="other">The snapshot to compare, or null.</param>
    /// <returns>True only when source, version, descriptor order, and descriptor content are equal.</returns>
    public bool Equals(ToolProviderSnapshot? other) => other is not null
        && SourceId == other.SourceId && SourceVersion == other.SourceVersion && Tools.SequenceEqual(other.Tools);

    /// <summary>Computes a hash compatible with ordered structural snapshot equality.</summary>
    /// <returns>A hash over the source, its version, and every retained descriptor.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SourceId);
        hash.Add(SourceVersion);
        foreach (var tool in Tools)
        {
            hash.Add(tool);
        }
        return hash.ToHashCode();
    }
}
