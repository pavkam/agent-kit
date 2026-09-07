// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a typed identity or authentication rejection before protected state is read.</summary>
public sealed record IdentityRejected: IdentityResolutionResult
{
    /// <summary>Initializes a rejected result.</summary>
    /// <param name="failure">The stable, credential-free failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public IdentityRejected(IdentityFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the identity failure.</summary>
    public IdentityFailure Failure { get; }
}
