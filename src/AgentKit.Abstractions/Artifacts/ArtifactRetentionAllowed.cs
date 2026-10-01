// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Permits the operation under one resolved retention decision.</summary>
public sealed record ArtifactRetentionAllowed: ArtifactRetentionDecision
{
    /// <summary>Initializes an allowing decision.</summary>
    /// <param name="retention">The resolved retention the finalized reference carries.</param>
    /// <exception cref="ArgumentNullException"><paramref name="retention"/> is null.</exception>
    public ArtifactRetentionAllowed(ArtifactRetention retention)
    {
        ArgumentNullException.ThrowIfNull(retention);
        Retention = retention;
    }

    /// <summary>Gets the resolved retention.</summary>
    public ArtifactRetention Retention { get; }
}
