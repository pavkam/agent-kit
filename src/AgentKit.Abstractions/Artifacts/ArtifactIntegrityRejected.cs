// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that observed content did not match its declaration.</summary>
public sealed record ArtifactIntegrityRejected: ArtifactIntegrityResult
{
    /// <summary>Initializes a rejected integrity check.</summary>
    /// <param name="failure">The stable non-sensitive failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public ArtifactIntegrityRejected(ArtifactFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the stable non-sensitive failure.</summary>
    public ArtifactFailure Failure { get; }
}
