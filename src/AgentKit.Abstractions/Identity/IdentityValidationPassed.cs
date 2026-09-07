// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that an immutable identity passed expiry, revocation, mapping, and assurance validation.</summary>
public sealed record IdentityValidationPassed: IdentityValidationResult
{
    /// <summary>Gets the shared stateless success value.</summary>
    public static IdentityValidationPassed Instance { get; } = new();

    private IdentityValidationPassed() { }
}
