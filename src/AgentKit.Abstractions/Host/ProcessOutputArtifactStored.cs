// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the portable reference for a durably preserved complete output stream.</summary>
public sealed record ProcessOutputArtifactStored: ProcessOutputArtifactResult
{
    /// <summary>Initializes a successful preservation result.</summary>
    /// <param name="reference">The committed portable reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public ProcessOutputArtifactStored(ArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Reference = reference;
    }

    /// <summary>Gets the committed portable reference.</summary>
    public ArtifactReference Reference { get; }
}
