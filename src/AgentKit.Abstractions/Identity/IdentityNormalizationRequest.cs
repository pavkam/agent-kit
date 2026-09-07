// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies an issuer-mapped candidate and its assertion to one additive normalization policy.</summary>
public sealed record IdentityNormalizationRequest
{
    /// <summary>Initializes a policy request.</summary>
    /// <param name="assertion">The original trusted assertion.</param>
    /// <param name="candidate">The current immutable candidate.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public IdentityNormalizationRequest(IdentityAssertion assertion, ExecutionIdentity candidate)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        ArgumentNullException.ThrowIfNull(candidate);
        Assertion = assertion;
        Candidate = candidate;
    }

    /// <summary>Gets the original trusted assertion.</summary>
    public IdentityAssertion Assertion { get; }
    /// <summary>Gets the current candidate.</summary>
    public ExecutionIdentity Candidate { get; }
}
