// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using AgentKit;

/// <summary>Creates internally consistent immutable security and session evidence for cross-package tests.</summary>
public static class TestSecurityEvidence
{
    /// <summary>Creates a tool context with matching authorization and optional session-profile evidence.</summary>
    /// <param name="agentId">The tool operation's agent.</param>
    /// <param name="sessionId">The optional session.</param>
    /// <param name="toolCallId">The causing tool call.</param>
    /// <param name="correlation">The exact causal operation.</param>
    /// <param name="identity">The complete execution identity.</param>
    /// <returns>An internally consistent test-only tool context.</returns>
    public static ToolExecutionContext ToolContext(
        AgentId agentId,
        SessionId? sessionId,
        ToolCallId toolCallId,
        OperationCorrelation correlation,
        ExecutionIdentity identity) => new(
            agentId,
            sessionId,
            toolCallId,
            correlation,
            identity,
            Authorization(agentId, sessionId, correlation, identity),
            sessionId is null ? null : SessionProfile());

    /// <summary>Creates a compaction context with matching authorization and session profile.</summary>
    /// <param name="compactionId">The checkpoint identity.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="correlation">The exact compaction operation.</param>
    /// <param name="identity">The complete execution identity.</param>
    /// <returns>An internally consistent test-only compaction context.</returns>
    public static CompactionOperationContext CompactionContext(
        CompactionId compactionId,
        AgentId agentId,
        SessionId sessionId,
        OperationCorrelation correlation,
        ExecutionIdentity identity) => new(
            compactionId,
            agentId,
            sessionId,
            correlation,
            identity,
            Authorization(agentId, sessionId, correlation, identity),
            SessionProfile());

    /// <summary>Creates captured authorization matching the supplied operation address and identity.</summary>
    /// <param name="agentId">The operation's agent.</param>
    /// <param name="sessionId">The operation's optional session.</param>
    /// <param name="correlation">The operation's exact causal correlation.</param>
    /// <param name="identity">The operation's complete execution identity.</param>
    /// <returns>Consistent immutable test-only authorization evidence.</returns>
    public static SecurityAuthorizationContext Authorization(
        AgentId agentId,
        SessionId? sessionId,
        OperationCorrelation correlation,
        ExecutionIdentity identity) => new(
            new SecurityProfileKey("test-security"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:test-policy")),
            new ComponentKey<ISecurityAuthority>("test-authority"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId, correlation),
            identity);

    /// <summary>Creates a deterministic immutable session profile for coordinator-facing tests.</summary>
    /// <param name="storeKey">The explicitly selected store key.</param>
    /// <returns>A process-local profile with positive bounded limits.</returns>
    public static SessionProfileSnapshot SessionProfile(string storeKey = "test-store") => new(
        new SessionProfileReference(new SessionProfileKey("test-session"), new SessionProfileVersion(1)),
        new ComponentKey<ISessionCoordinator>("test-coordinator"),
        new ComponentKey<ISessionRunCoordinator>("test-run-coordinator"),
        new SessionStoreKey(storeKey),
        SessionStoreCapabilities.None,
        requiresDurableStore: false,
        requiresDistributedFencing: false,
        new SessionRetentionProfileKey("test-retention"),
        SessionBusyBehavior.Reject,
        maximumAppendEntries: 128,
        maximumPageSize: 256,
        verifySnapshotHashes: true,
        deleteOnDispose: false,
        new ContentHash("sha256:test-session-profile"));
}
