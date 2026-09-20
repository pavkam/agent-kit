// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>
/// The projected state of one session: its identity facts, every branch it
/// has ever forked, and the idempotency caches for creation and branching.
/// </summary>
/// <remarks>
/// This is a projection of the durable record log, not the authoritative record itself: the log is appended and flushed
/// first, and replay rebuilds an identical instance. All access is serialized by <see cref="JsonSessionStore"/>'s single
/// store-wide gate; this class performs no synchronization of its own.
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
    public SessionRecord(
        SessionAddress address,
        ConversationId? conversationId,
        TenantId tenantId,
        PrincipalId ownerId,
        BranchId activeBranchId,
        DateTimeOffset createdAt)
    {
        Address = address;
        ConversationId = conversationId;
        TenantId = tenantId;
        OwnerId = ownerId;
        ActiveBranchId = activeBranchId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>Gets the session's complete address.</summary>
    public SessionAddress Address { get; }

    /// <summary>Gets the optional higher-level conversation this session belongs to.</summary>
    public ConversationId? ConversationId { get; }

    /// <summary>Gets the tenant that owns the session.</summary>
    public TenantId TenantId { get; }

    /// <summary>Gets the principal that created the session.</summary>
    public PrincipalId OwnerId { get; }

    /// <summary>Gets the time the session was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets or sets the currently active branch.</summary>
    public BranchId ActiveBranchId { get; set; }

    /// <summary>Gets or sets the time the session was last mutated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the session's lifecycle state.</summary>
    public SessionLifecycleState State { get; set; } = SessionLifecycleState.Active;

    /// <summary>Gets or sets the canonical whole-session compare-and-swap version.</summary>
    public long Version { get; set; }

    /// <summary>Gets every branch this session has ever forked, keyed by branch identity.</summary>
    public Dictionary<BranchId, BranchRecord> Branches { get; } = [];

    /// <summary>
    /// Gets the cache of previously accepted branch-creation results, keyed
    /// by idempotency key.
    /// </summary>
    public Dictionary<IdempotencyKey, IdempotencyReceipt<SessionBranchRequest, SessionBranched>> BranchIdempotency { get; } = [];

    /// <summary>Gets canonical admitted inputs keyed by caller idempotency identity.</summary>
    public Dictionary<InputId, StoredAdmission> AdmissionsByInput { get; } = [];

    /// <summary>Gets canonical admitted inputs keyed by runtime admission identity.</summary>
    public Dictionary<AdmissionId, StoredAdmission> AdmissionsById { get; } = [];

    /// <summary>Gets exact input-admission commit receipts keyed by transaction idempotency identity.</summary>
    public Dictionary<IdempotencyKey, IdempotencyReceipt<SessionInputAdmissionRequest, AcceptedInput>> AdmissionIdempotency { get; } = [];

    /// <summary>Gets lane state keyed by resolved execution lane.</summary>
    public Dictionary<ExecutionLaneId, LaneRecord> Lanes { get; } = [];

    /// <summary>Gets successful lane-provision receipts keyed by their exact idempotency key.</summary>
    public Dictionary<IdempotencyKey, IdempotencyReceipt<SessionExecutionLaneProvisionRequest, SessionExecutionLaneProvisioned>> LaneProvisionIdempotency { get; } = [];

    /// <summary>Gets every globally reserved session-entry identity.</summary>
    public HashSet<SessionEntryId> EntryIds { get; } = [];

    /// <summary>Gets every globally reserved message identity materialized in session history.</summary>
    public HashSet<MessageId> MessageIds { get; } = [];

    /// <summary>Gets successful run-start receipts keyed by start idempotency identity.</summary>
    public Dictionary<IdempotencyKey, RunStartReceipt> RunStartIdempotency { get; } = [];

    /// <summary>Gets successful lane-release receipts keyed by release idempotency identity.</summary>
    public Dictionary<IdempotencyKey, IdempotencyReceipt<SessionRunReleaseRequest, SessionRunReleased>> RunReleaseIdempotency { get; } = [];

    /// <summary>Gets successful durable-abort receipts keyed by abort idempotency identity.</summary>
    public Dictionary<IdempotencyKey, IdempotencyReceipt<SessionRunAbortRequest, SessionRunAbortRecorded>> RunAbortIdempotency { get; } = [];

    /// <summary>Gets successful mid-run input-promotion receipts keyed by promotion idempotency identity.</summary>
    public Dictionary<IdempotencyKey, InputPromotionReceipt> InputPromotionIdempotency { get; } = [];
}
