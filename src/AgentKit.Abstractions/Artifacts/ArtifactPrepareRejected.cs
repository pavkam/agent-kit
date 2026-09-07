// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Rejects staging before content becomes publishable.</summary>
public sealed record ArtifactPrepareRejected: ArtifactPrepareResult
{
    /// <summary>Initializes a rejected staging result.</summary>
    /// <param name="failure">The typed safe failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public ArtifactPrepareRejected(ArtifactFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }
    /// <summary>Gets the typed safe failure.</summary>
    public ArtifactFailure Failure { get; }
}
