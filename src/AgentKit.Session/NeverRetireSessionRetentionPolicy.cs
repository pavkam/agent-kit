// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>
/// A safe, documented default retention policy that always decides
/// <see cref="SessionRetentionAction.Keep"/>.
/// </summary>
/// <remarks>
/// Deployments that need archival or deletion register their own
/// <see cref="ISessionRetentionPolicy"/> through
/// <c>ReplaceSessionRetentionPolicy</c>; AgentKit never fabricates a
/// deletion policy, since destroying durable conversation history is a
/// consequential, deployment-specific decision.
/// </remarks>
public sealed class NeverRetireSessionRetentionPolicy: ISessionRetentionPolicy
{
    /// <inheritdoc/>
    public ValueTask<SessionRetentionDecision> EvaluateAsync(
        SessionDescriptor session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ValueTask.FromResult(new SessionRetentionDecision(SessionRetentionAction.Keep, null));
    }
}
