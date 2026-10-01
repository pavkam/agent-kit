// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the complete content was committed as an immutable artifact.</summary>
public sealed record ToolResultSpilled: ToolResultSpillResult
{
    /// <summary>Initializes a successful spill.</summary>
    /// <param name="reference">The committed artifact reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public ToolResultSpilled(ArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Reference = reference;
    }

    /// <summary>Gets the committed artifact reference.</summary>
    public ArtifactReference Reference { get; }
}
