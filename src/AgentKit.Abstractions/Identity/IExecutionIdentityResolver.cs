// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Normalizes a trusted adapter assertion into an immutable execution identity.</summary>
public interface IExecutionIdentityResolver
{
    /// <summary>Resolves and validates one trusted assertion without authenticating raw credentials.</summary>
    /// <param name="assertion">The bounded assertion produced by a trusted ingress adapter.</param>
    /// <param name="cancellationToken">Signals that the caller no longer needs resolution.</param>
    /// <returns>A successful identity or typed rejection.</returns>
    public ValueTask<IdentityResolutionResult> ResolveAsync(IdentityAssertion assertion, CancellationToken cancellationToken = default);
}
