// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>
/// One session's row-backed identity facts: address, tenancy, active branch, lifecycle, and the
/// canonical whole-session optimistic-concurrency version.
/// </summary>
/// <remarks>
/// This is a query-result / write-parameter shape for exactly the <c>agentkit_sessions</c> row.
/// Its branches, entries, lanes, admissions, and idempotency receipts each live in their own table
/// and are read or written independently through <see cref="SqliteSessionUnitOfWork"/>; loading or
/// persisting one session's metadata never touches another session's rows.
/// </remarks>
internal sealed class SessionRecord
{
    /// <summary>Initializes a new instance of the <see cref="SessionRecord"/> class.</summary>
    /// <param name="address">The session's complete address.</param>
    /// <param name="conversationId">The optional higher-level conversation this session belongs to.</param>
    /// <param name="tenantId">The tenant that owns the session.</param>
    /// <param name="ownerId">The principal that created the session.</param>
    /// <param name="activeBranchId">The session's initial (and currently active) branch.</param>
    /// <param name="createdAt">The time the session was created.</param>
    /// <param name="updatedAt">The time the session was last mutated.</param>
    /// <param name="state">The session's lifecycle state.</param>
    /// <param name="version">The canonical whole-session compare-and-swap version.</param>
    internal SessionRecord(
        SessionAddress address,
        ConversationId? conversationId,
        TenantId tenantId,
        PrincipalId ownerId,
        BranchId activeBranchId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        SessionLifecycleState state,
        long version)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
        ConversationId = conversationId;
        TenantId = tenantId;
        OwnerId = ownerId;
        ActiveBranchId = activeBranchId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        State = state;
        Version = version;
    }

    /// <summary>Gets the session's complete address.</summary>
    internal SessionAddress Address { get; }

    /// <summary>Gets the optional higher-level conversation this session belongs to.</summary>
    internal ConversationId? ConversationId { get; }

    /// <summary>Gets the tenant that owns the session.</summary>
    internal TenantId TenantId { get; }

    /// <summary>Gets the principal that created the session.</summary>
    internal PrincipalId OwnerId { get; }

    /// <summary>Gets the currently active branch.</summary>
    internal BranchId ActiveBranchId { get; }

    /// <summary>Gets the time the session was created.</summary>
    internal DateTimeOffset CreatedAt { get; }

    /// <summary>Gets or sets the time the session was last mutated.</summary>
    internal DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets the session's lifecycle state.</summary>
    internal SessionLifecycleState State { get; }

    /// <summary>Gets or sets the canonical whole-session compare-and-swap version.</summary>
    internal long Version { get; set; }
}
