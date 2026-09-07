// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a typed validation rejection before protected state is exposed.</summary>
public sealed record IdentityValidationRejected: IdentityValidationResult
{
    /// <summary>Initializes a rejected validation result.</summary>
    /// <param name="failure">The safe identity failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public IdentityValidationRejected(IdentityFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the validation failure.</summary>
    public IdentityFailure Failure { get; }
}
