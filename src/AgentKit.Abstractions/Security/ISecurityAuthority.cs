// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Evaluates normalized protected operations and alone issues bounded security grants.</summary>
public interface ISecurityAuthority
{
    /// <summary>Authorizes one normalized protected operation without performing its effect.</summary>
    /// <param name="request">The complete normalized request.</param>
    /// <param name="cancellationToken">Cancels policy evaluation and grant issue.</param>
    /// <returns>The closed allow-or-deny decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default);
}
