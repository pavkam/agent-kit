// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Refuses the operation because retention policy, hold, or ownership prohibits it.</summary>
public sealed record ArtifactRetentionRejected: ArtifactRetentionDecision
{
    /// <summary>Initializes a rejecting decision.</summary>
    /// <param name="failure">The stable non-sensitive failure, normally <see cref="ArtifactFailureKind.RetentionConflict"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public ArtifactRetentionRejected(ArtifactFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the stable non-sensitive failure.</summary>
    public ArtifactFailure Failure { get; }
}
