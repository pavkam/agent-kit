// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The current, versioned identity and lifecycle state of one session,
/// independent of its entry content.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. A fresh descriptor is returned by every
/// operation that changes session state (create, append, branch, delete),
/// so callers observe the authoritative <see cref="Version"/> without a
/// separate read.
/// </remarks>
public sealed record SessionDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="SessionDescriptor"/> record.</summary>
    /// <param name="address">The session's complete address.</param>
    /// <param name="conversationId">The optional higher-level conversation this session belongs to.</param>
    /// <param name="tenantId">The tenant that owns the session.</param>
    /// <param name="ownerId">The principal that created the session.</param>
    /// <param name="storeKey">The store this session's record is persisted in.</param>
    /// <param name="activeBranchId">The currently active branch.</param>
    /// <param name="version">The optimistic-concurrency version of the active branch.</param>
    /// <param name="state">The session's lifecycle state.</param>
    /// <param name="createdAt">The time the session was created.</param>
    /// <param name="updatedAt">The time the session was last mutated.</param>
    /// <param name="schemaVersion">The durable schema version of this descriptor.</param>
    /// <param name="extensions">Store-specific or forward-compatible descriptor data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> or <paramref name="extensions"/> is null.
    /// </exception>
    public SessionDescriptor(
        SessionAddress address,
        ConversationId? conversationId,
        TenantId tenantId,
        PrincipalId ownerId,
        SessionStoreKey storeKey,
        BranchId activeBranchId,
        SessionVersion version,
        SessionLifecycleState state,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        SchemaVersion schemaVersion,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(extensions);

        Address = address;
        ConversationId = conversationId;
        TenantId = tenantId;
        OwnerId = ownerId;
        StoreKey = storeKey;
        ActiveBranchId = activeBranchId;
        Version = version;
        State = state;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        SchemaVersion = schemaVersion;
        Extensions = extensions;
    }

    /// <summary>Gets the session's complete address.</summary>
    public SessionAddress Address { get; init; }

    /// <summary>Gets the optional higher-level conversation this session belongs to.</summary>
    public ConversationId? ConversationId { get; init; }

    /// <summary>Gets the tenant that owns the session.</summary>
    public TenantId TenantId { get; init; }

    /// <summary>Gets the principal that created the session.</summary>
    public PrincipalId OwnerId { get; init; }

    /// <summary>Gets the store this session's record is persisted in.</summary>
    public SessionStoreKey StoreKey { get; init; }

    /// <summary>Gets the currently active branch.</summary>
    public BranchId ActiveBranchId { get; init; }

    /// <summary>Gets the optimistic-concurrency version of the active branch.</summary>
    public SessionVersion Version { get; init; }

    /// <summary>Gets the session's lifecycle state.</summary>
    public SessionLifecycleState State { get; init; }

    /// <summary>Gets the time the session was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the time the session was last mutated.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the durable schema version of this descriptor.</summary>
    public SchemaVersion SchemaVersion { get; init; }

    /// <summary>Gets store-specific or forward-compatible descriptor data.</summary>
    public ExtensionData Extensions { get; init; }
}
