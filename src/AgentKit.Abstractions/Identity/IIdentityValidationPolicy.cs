// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates normalized identity evidence, mapping, assurance, and delegation invariants.</summary>
public interface IIdentityValidationPolicy
{
    /// <summary>Validates one immutable candidate without granting authority.</summary>
    /// <param name="identity">The candidate to validate.</param>
    /// <param name="cancellationToken">Signals that the caller no longer needs validation.</param>
    /// <returns>A success marker or typed identity rejection.</returns>
    public ValueTask<IdentityValidationResult> ValidateAsync(ExecutionIdentity identity, CancellationToken cancellationToken = default);
}
