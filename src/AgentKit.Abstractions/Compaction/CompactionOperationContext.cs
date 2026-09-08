// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable operation context threaded through one compaction attempt:
/// which checkpoint, which session, whose causal operation, and on whose
/// behalf.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. <see cref="CompactionId"/> identifies the
/// logical checkpoint and stays stable across a retried attempt, while
/// <see cref="Correlation"/>'s <c>OperationId</c> identifies this specific
/// invocation.
/// </remarks>
public sealed record CompactionOperationContext
{
    /// <summary>Initializes a new instance of the <see cref="CompactionOperationContext"/> record.</summary>
    /// <param name="compactionId">The logical checkpoint this attempt belongs to.</param>
    /// <param name="agentId">The agent that owns the session being compacted.</param>
    /// <param name="sessionId">The session being compacted.</param>
    /// <param name="correlation">The causal operation performing this attempt.</param>
    /// <param name="identity">The identity on whose behalf this attempt is performed.</param>
    /// <param name="authorization">The exact captured authorization for this compaction operation.</param>
    /// <param name="sessionProfile">The immutable session profile used for the compaction's session calls.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlation"/>, <paramref name="identity"/>,
    /// <paramref name="authorization"/>, or <paramref name="sessionProfile"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="authorization"/> does not exactly match the supplied agent,
    /// session, correlation, and complete execution identity.
    /// </exception>
    public CompactionOperationContext(
        CompactionId compactionId,
        AgentId agentId,
        SessionId sessionId,
        OperationCorrelation correlation,
        ExecutionIdentity identity,
        SecurityAuthorizationContext authorization,
        SessionProfileSnapshot sessionProfile)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfInvalidOperationAuthorization(identity, agentId, sessionId, correlation,
            authorization);
        ArgumentNullException.ThrowIfNull(sessionProfile);

        CompactionId = compactionId;
        AgentId = agentId;
        SessionId = sessionId;
        Correlation = correlation;
        Identity = identity;
        Authorization = authorization;
        SessionProfile = sessionProfile;
    }

    /// <summary>Gets the logical checkpoint this attempt belongs to.</summary>
    public CompactionId CompactionId { get; }

    /// <summary>Gets the agent that owns the session being compacted.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the session being compacted.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the causal operation performing this attempt.</summary>
    public OperationCorrelation Correlation { get; }

    /// <summary>Gets the identity on whose behalf this attempt is performed.</summary>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the exact authorization captured for this compaction operation.</summary>
    /// <value>Immutable evidence matching the complete identity, session address, and correlation.</value>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the immutable session profile used by this compaction operation.</summary>
    /// <value>The exact compiled profile passed to every coordinator call.</value>
    public SessionProfileSnapshot SessionProfile { get; }
}
