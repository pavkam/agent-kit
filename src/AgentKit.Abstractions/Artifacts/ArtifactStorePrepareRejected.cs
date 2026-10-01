// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a typed, content-free failure from the store before any staging became visible.</summary>
public sealed record ArtifactStorePrepareRejected: ArtifactStorePrepareResult
{
    /// <summary>Initializes a store rejection.</summary>
    /// <param name="failure">The stable non-sensitive failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public ArtifactStorePrepareRejected(ArtifactFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the stable non-sensitive failure.</summary>
    public ArtifactFailure Failure { get; }
}
