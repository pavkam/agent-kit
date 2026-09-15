// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Captures the complete bounded semantic store state written in one SQLite transaction.</summary>
internal sealed class SqliteSessionStoreState
{
    /// <summary>Initializes an empty state used by deserialization.</summary>
    public SqliteSessionStoreState()
    {
    }

    /// <summary>Gets session records by complete address.</summary>
    public Dictionary<SessionAddress, SessionRecord> Sessions { get; set; } = [];
    /// <summary>Gets exact successful creation receipts by tenant, agent, and retry key.</summary>
    public Dictionary<(TenantId, AgentId, IdempotencyKey), IdempotencyReceipt<SessionStoreCreateRequest, SessionCreated>> CreateIdempotency { get; set; } = [];
    /// <summary>Gets deleted creation requests retained to prevent retry-key resurrection.</summary>
    public Dictionary<(TenantId, AgentId, IdempotencyKey), SessionStoreCreateRequest> DeletedCreateIdempotency { get; set; } = [];
    /// <summary>Gets exact deletion receipts by tenant, address, and retry key.</summary>
    public Dictionary<(TenantId, SessionAddress, IdempotencyKey), IdempotencyReceipt<SessionDeleteRequest, SessionDeleted>> DeleteIdempotency { get; set; } = [];
}
