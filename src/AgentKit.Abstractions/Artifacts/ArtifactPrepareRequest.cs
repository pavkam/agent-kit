// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests bounded staging of complete artifact content without publishing it.</summary>
public sealed record ArtifactPrepareRequest
{
    /// <summary>Initializes one staging request.</summary>
    /// <param name="agentId">The acting agent.</param>
    /// <param name="sessionId">The optional owning session.</param>
    /// <param name="toolCallId">The causing tool call, when applicable.</param>
    /// <param name="correlation">The causal operation.</param>
    /// <param name="identity">The authenticated identity.</param>
    /// <param name="directoryId">The logical directory.</param>
    /// <param name="metadata">The declared content and policy.</param>
    /// <param name="content">The readable caller-owned stream retained only until prepare returns.</param>
    /// <param name="idempotencyKey">The caller-owned replay key.</param>
    /// <exception cref="ArgumentException">A value is blank or <paramref name="content"/> is unreadable.</exception>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    public ArtifactPrepareRequest(
        AgentId agentId, SessionId? sessionId, ToolCallId? toolCallId,
        OperationCorrelation correlation, ExecutionIdentity identity,
        ArtifactDirectoryId directoryId, ArtifactMetadata metadata,
        Stream content, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryId.Value, nameof(directoryId));
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNotReadable(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        AgentId = agentId; SessionId = sessionId; ToolCallId = toolCallId;
        Correlation = correlation; Identity = identity; DirectoryId = directoryId;
        Metadata = metadata; Content = content; IdempotencyKey = idempotencyKey;
    }
    /// <summary>Gets the acting agent.</summary>
    public AgentId AgentId { get; }
    /// <summary>Gets the optional owning session.</summary>
    public SessionId? SessionId { get; }
    /// <summary>Gets the causing tool call.</summary>
    public ToolCallId? ToolCallId { get; }
    /// <summary>Gets the causal operation.</summary>
    public OperationCorrelation Correlation { get; }
    /// <summary>Gets the authenticated identity.</summary>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the logical directory.</summary>
    public ArtifactDirectoryId DirectoryId { get; }
    /// <summary>Gets declared content and policy.</summary>
    public ArtifactMetadata Metadata { get; }
    /// <summary>Gets the caller-owned readable stream.</summary>
    public Stream Content { get; }
    /// <summary>Gets the caller-owned replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
