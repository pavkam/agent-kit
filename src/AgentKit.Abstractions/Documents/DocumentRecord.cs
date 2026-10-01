// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one immutable version of a source document with its owner, visibility, and content identity.</summary>
/// <remarks>Each version of a document is its own record sharing <see cref="Id"/>. The content hash and version identify the source so citations and stale-index filtering stay exact; tenant and owner fields are routing and visibility projections, never authority.</remarks>
public sealed record DocumentRecord
{
    /// <summary>Initializes a validated record.</summary>
    /// <param name="id">The document identity shared by every version.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sourceSessionId">The session that produced the document.</param>
    /// <param name="sourceRunId">The run that produced the document.</param>
    /// <param name="tenantId">The owning tenant; it must equal the visibility's tenant.</param>
    /// <param name="visibility">The principal visibility.</param>
    /// <param name="version">The source version.</param>
    /// <param name="contentHash">The hash identifying the exact source content.</param>
    /// <param name="metadata">The document metadata.</param>
    /// <param name="classification">The sensitivity.</param>
    /// <param name="provenance">The source evidence.</param>
    /// <param name="retention">The retention policy.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or the classification is undefined.</exception>
    /// <exception cref="ArgumentException">A key, version, or hash is blank, or the tenant disagrees with the visibility.</exception>
    public DocumentRecord(
        DocumentId id,
        AgentId agentId,
        SessionId sourceSessionId,
        RunId sourceRunId,
        TenantId tenantId,
        PrincipalVisibility visibility,
        DocumentVersion version,
        ContentHash contentHash,
        DocumentMetadata metadata,
        DataClassification classification,
        Provenance provenance,
        RetentionPolicy retention)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfEqual(sourceSessionId, default, nameof(sourceSessionId));
        ArgumentOutOfRangeException.ThrowIfEqual(sourceRunId, default, nameof(sourceRunId));
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentException.ThrowIfNotEqual(visibility.TenantId, tenantId, nameof(visibility));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash.Value, nameof(contentHash));
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentOutOfRangeException.ThrowIfUndefined(classification);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(retention);
        Id = id;
        AgentId = agentId;
        SourceSessionId = sourceSessionId;
        SourceRunId = sourceRunId;
        TenantId = tenantId;
        Visibility = visibility;
        Version = version;
        ContentHash = contentHash;
        Metadata = metadata;
        Classification = classification;
        Provenance = provenance;
        Retention = retention;
    }

    /// <summary>Gets the document identity shared by every version.</summary>
    public DocumentId Id { get; }

    /// <summary>Gets the owning agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the session that produced the document.</summary>
    public SessionId SourceSessionId { get; }

    /// <summary>Gets the run that produced the document.</summary>
    public RunId SourceRunId { get; }

    /// <summary>Gets the owning tenant.</summary>
    public TenantId TenantId { get; }

    /// <summary>Gets the principal visibility.</summary>
    public PrincipalVisibility Visibility { get; }

    /// <summary>Gets the source version.</summary>
    public DocumentVersion Version { get; }

    /// <summary>Gets the hash identifying the exact source content.</summary>
    public ContentHash ContentHash { get; }

    /// <summary>Gets the document metadata.</summary>
    public DocumentMetadata Metadata { get; }

    /// <summary>Gets the sensitivity.</summary>
    public DataClassification Classification { get; }

    /// <summary>Gets the source evidence.</summary>
    public Provenance Provenance { get; }

    /// <summary>Gets the retention policy.</summary>
    public RetentionPolicy Retention { get; }
}
