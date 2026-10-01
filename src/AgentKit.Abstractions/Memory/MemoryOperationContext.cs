// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one memory, document, or retrieval operation to its agent, session, authenticated identity, causal correlation, authorization snapshot, and exact memory profile.</summary>
/// <remarks>
/// Construction rejects disagreement between the address, identity, correlation, and the authorization snapshot so a component
/// can never act under evidence that names another agent, session, or principal. The context is immutable and carries no
/// authority by itself: stores and sources still validate a bounded <see cref="SecurityGrant"/> before every effect.
/// </remarks>
public sealed record MemoryOperationContext
{
    /// <summary>Initializes a validated operation context.</summary>
    /// <param name="agentId">The agent the operation runs for.</param>
    /// <param name="sessionId">The session the operation runs in.</param>
    /// <param name="identity">The complete authenticated execution identity.</param>
    /// <param name="correlation">The operation causal correlation.</param>
    /// <param name="authorization">The authorization snapshot whose scope, identity, and correlation must equal the other arguments.</param>
    /// <param name="profileKey">The memory profile the operation was compiled under.</param>
    /// <param name="profileVersion">The exact profile version.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity value or the profile version is default or not positive.</exception>
    /// <exception cref="ArgumentException">The profile key is blank, or the authorization does not exactly bind the agent, session, identity, and correlation.</exception>
    public MemoryOperationContext(
        AgentId agentId,
        SessionId sessionId,
        ExecutionIdentity identity,
        OperationCorrelation correlation,
        SecurityAuthorizationContext authorization,
        MemoryProfileKey profileKey,
        MemoryProfileVersion profileVersion)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileVersion.Value, nameof(profileVersion));
        ArgumentException.ThrowIfInvalidOperationAuthorization(identity, agentId, sessionId, correlation, authorization, nameof(authorization));
        AgentId = agentId;
        SessionId = sessionId;
        Identity = identity;
        Correlation = correlation;
        Authorization = authorization;
        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
    }

    /// <summary>Gets the agent the operation runs for.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the session the operation runs in.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the complete authenticated execution identity.</summary>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the operation causal correlation.</summary>
    public OperationCorrelation Correlation { get; }

    /// <summary>Gets the authorization snapshot the operation runs under.</summary>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the memory profile the operation was compiled under.</summary>
    public MemoryProfileKey ProfileKey { get; }

    /// <summary>Gets the exact memory profile version.</summary>
    public MemoryProfileVersion ProfileVersion { get; }
}
