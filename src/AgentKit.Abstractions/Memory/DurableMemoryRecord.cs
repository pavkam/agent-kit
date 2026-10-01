// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one durable memory with its owner, visibility, lifecycle state, and optimistic version.</summary>
/// <remarks>
/// Tenant and owner fields are normalized routing and visibility projections; they never replace the authenticated operation
/// identity that authorized a read or write. A record is immutable: lifecycle changes produce a new value through the store.
/// </remarks>
public sealed record DurableMemoryRecord
{
    /// <summary>Initializes a validated record.</summary>
    /// <param name="id">The memory identity.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sourceSessionId">The session that produced the content.</param>
    /// <param name="sourceRunId">The run that produced the content.</param>
    /// <param name="namespace">The namespace within the agent's partition.</param>
    /// <param name="tenantId">The owning tenant; it must equal the visibility's tenant.</param>
    /// <param name="visibility">The principal visibility.</param>
    /// <param name="kind">The semantic role.</param>
    /// <param name="content">The body.</param>
    /// <param name="classification">The sensitivity.</param>
    /// <param name="provenance">The source evidence.</param>
    /// <param name="retention">The retention policy.</param>
    /// <param name="state">The lifecycle state.</param>
    /// <param name="version">The optimistic version token.</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="updatedAt">The last-update instant, not earlier than <paramref name="createdAt"/>.</param>
    /// <param name="extensions">Additional extension fields.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, an enumeration is undefined, or the update precedes creation.</exception>
    /// <exception cref="ArgumentException">A key or version is blank, or the tenant disagrees with the visibility.</exception>
    public DurableMemoryRecord(
        MemoryId id,
        AgentId agentId,
        SessionId sourceSessionId,
        RunId sourceRunId,
        MemoryNamespace @namespace,
        TenantId tenantId,
        PrincipalVisibility visibility,
        MemoryKind kind,
        MemoryContent content,
        DataClassification classification,
        Provenance provenance,
        RetentionPolicy retention,
        MemoryLifecycleState state,
        VersionToken version,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfEqual(sourceSessionId, default, nameof(sourceSessionId));
        ArgumentOutOfRangeException.ThrowIfEqual(sourceRunId, default, nameof(sourceRunId));
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace.Value, nameof(@namespace));
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentException.ThrowIfNotEqual(visibility.TenantId, tenantId, nameof(visibility));
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfUndefined(classification);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentOutOfRangeException.ThrowIfUndefined(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentOutOfRangeException.ThrowIfLessThan(updatedAt, createdAt, nameof(updatedAt));
        ArgumentNullException.ThrowIfNull(extensions);
        Id = id;
        AgentId = agentId;
        SourceSessionId = sourceSessionId;
        SourceRunId = sourceRunId;
        Namespace = @namespace;
        TenantId = tenantId;
        Visibility = visibility;
        Kind = kind;
        Content = content;
        Classification = classification;
        Provenance = provenance;
        Retention = retention;
        State = state;
        Version = version;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Extensions = extensions;
    }

    /// <summary>Gets the memory identity.</summary>
    public MemoryId Id { get; }

    /// <summary>Gets the owning agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the session that produced the content.</summary>
    public SessionId SourceSessionId { get; }

    /// <summary>Gets the run that produced the content.</summary>
    public RunId SourceRunId { get; }

    /// <summary>Gets the namespace within the agent's partition.</summary>
    public MemoryNamespace Namespace { get; }

    /// <summary>Gets the owning tenant.</summary>
    public TenantId TenantId { get; }

    /// <summary>Gets the principal visibility.</summary>
    public PrincipalVisibility Visibility { get; }

    /// <summary>Gets the semantic role.</summary>
    public MemoryKind Kind { get; }

    /// <summary>Gets the body.</summary>
    public MemoryContent Content { get; }

    /// <summary>Gets the sensitivity.</summary>
    public DataClassification Classification { get; }

    /// <summary>Gets the source evidence.</summary>
    public Provenance Provenance { get; }

    /// <summary>Gets the retention policy.</summary>
    public RetentionPolicy Retention { get; }

    /// <summary>Gets the lifecycle state.</summary>
    public MemoryLifecycleState State { get; }

    /// <summary>Gets the optimistic version token.</summary>
    public VersionToken Version { get; }

    /// <summary>Gets the creation instant.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets the last-update instant.</summary>
    public DateTimeOffset UpdatedAt { get; }

    /// <summary>Gets additional extension fields.</summary>
    public ExtensionData Extensions { get; }

    /// <summary>Creates the record a store keeps after a lifecycle change.</summary>
    /// <param name="state">The new lifecycle state.</param>
    /// <param name="version">The new version token.</param>
    /// <param name="updatedAt">The change instant.</param>
    /// <param name="content">The replacement body, or <see langword="null"/> to keep the current body.</param>
    /// <returns>A validated copy that differs only in the supplied parts.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is undefined or <paramref name="updatedAt"/> precedes the current update.</exception>
    /// <exception cref="ArgumentException"><paramref name="version"/> is blank.</exception>
    public DurableMemoryRecord WithState(MemoryLifecycleState state, VersionToken version, DateTimeOffset updatedAt, MemoryContent? content = null) =>
        new(Id, AgentId, SourceSessionId, SourceRunId, Namespace, TenantId, Visibility, Kind, content ?? Content, Classification,
            Provenance, Retention, state, version, CreatedAt, updatedAt < UpdatedAt ? UpdatedAt : updatedAt, Extensions);
}
