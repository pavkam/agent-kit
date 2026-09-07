// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Rejects removal without claiming staging content is absent.</summary>
public sealed record ArtifactAbortRejected: ArtifactAbortResult
{
    /// <summary>Initializes a rejected abort.</summary>
    /// <param name="failure">The typed safe failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public ArtifactAbortRejected(ArtifactFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }
    /// <summary>Gets the typed safe failure.</summary>
    public ArtifactFailure Failure { get; }
}
