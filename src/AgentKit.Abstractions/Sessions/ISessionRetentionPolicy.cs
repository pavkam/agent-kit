// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Decides whether a session is eligible for archival or deletion.
/// </summary>
/// <remarks>
/// The policy only decides eligibility; it never performs the mutation
/// itself. A stateless, deterministic implementation may be registered as a
/// singleton.
/// </remarks>
public interface ISessionRetentionPolicy
{
    /// <summary>Evaluates one session against this policy.</summary>
    /// <param name="session">The session descriptor to evaluate.</param>
    /// <param name="cancellationToken">A token used to cancel the evaluation.</param>
    /// <returns>A task producing the retention decision.</returns>
    public ValueTask<SessionRetentionDecision> EvaluateAsync(
        SessionDescriptor session,
        CancellationToken cancellationToken = default);
}
