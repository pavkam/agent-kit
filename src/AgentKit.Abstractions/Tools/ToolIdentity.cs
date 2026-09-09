// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one exact published version of a canonical tool.</summary>
/// <remarks>This pair is produced by catalog resolution and is distinct from the provider-visible <see cref="ToolAlias"/>.</remarks>
public readonly record struct ToolIdentity
{
    /// <summary>Initializes an exact canonical tool identity.</summary>
    /// <param name="id">The nondefault canonical tool identity.</param>
    /// <param name="version">The nondefault published tool version.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> or <paramref name="version"/> is default.</exception>
    public ToolIdentity(ToolId id, ToolVersion version)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        Id = id;
        Version = version;
    }

    /// <summary>Gets the canonical tool identity.</summary>
    /// <value>A nondefault identity established by catalog resolution.</value>
    public ToolId Id { get; }

    /// <summary>Gets the exact published tool version.</summary>
    /// <value>A nondefault version captured with the catalog snapshot.</value>
    public ToolVersion Version { get; }
}
