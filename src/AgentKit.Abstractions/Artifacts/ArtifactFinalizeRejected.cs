// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Rejects publication while leaving no falsely committed reference.</summary>
public sealed record ArtifactFinalizeRejected: ArtifactFinalizeResult
{
    /// <summary>Initializes a rejected publication.</summary>
    /// <param name="failure">The typed safe failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public ArtifactFinalizeRejected(ArtifactFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }
    /// <summary>Gets the typed safe failure.</summary>
    public ArtifactFailure Failure { get; }
}
