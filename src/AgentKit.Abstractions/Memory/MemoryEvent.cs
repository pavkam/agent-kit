// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one immutable, content-free observation of a memory or retrieval transition.</summary>
/// <remarks>Events carry identities, a bounded outcome, and counts, never memory text, queries, or candidate content. Sinks observe events and cannot affect policy, ranking, or results.</remarks>
public sealed record MemoryEvent
{
    /// <summary>Initializes a validated event.</summary>
    /// <param name="kind">The defined event kind.</param>
    /// <param name="tenantId">The tenant concerned.</param>
    /// <param name="agentId">The agent concerned.</param>
    /// <param name="sessionId">The session concerned.</param>
    /// <param name="profileKey">The profile concerned.</param>
    /// <param name="profileVersion">The exact profile version.</param>
    /// <param name="memoryId">The memory concerned, or <see langword="null"/>.</param>
    /// <param name="documentId">The document concerned, or <see langword="null"/>.</param>
    /// <param name="retrievalRequestId">The retrieval request concerned, or <see langword="null"/>.</param>
    /// <param name="outcome">The non-blank bounded outcome name.</param>
    /// <param name="count">A bounded nonnegative count such as the candidates exposed, or <see langword="null"/>.</param>
    /// <param name="occurredAt">The instant of the transition.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind is undefined, an identity is default, the version is not positive, or the count is negative.</exception>
    /// <exception cref="ArgumentException">A key, tenant, or outcome is blank.</exception>
    public MemoryEvent(
        MemoryEventKind kind,
        TenantId tenantId,
        AgentId agentId,
        SessionId sessionId,
        MemoryProfileKey profileKey,
        MemoryProfileVersion profileVersion,
        MemoryId? memoryId,
        DocumentId? documentId,
        RetrievalRequestId? retrievalRequestId,
        string outcome,
        int? count,
        DateTimeOffset occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileVersion.Value, nameof(profileVersion));
        if (memoryId is { } memory)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(memory, default, nameof(memoryId));
        }

        if (documentId is { } document)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(document, default, nameof(documentId));
        }

        if (retrievalRequestId is { } request)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(request, default, nameof(retrievalRequestId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        if (count is { } observed)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(observed, nameof(count));
        }

        Kind = kind;
        TenantId = tenantId;
        AgentId = agentId;
        SessionId = sessionId;
        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
        MemoryId = memoryId;
        DocumentId = documentId;
        RetrievalRequestId = retrievalRequestId;
        Outcome = outcome;
        Count = count;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the event kind.</summary>
    public MemoryEventKind Kind { get; }

    /// <summary>Gets the tenant concerned.</summary>
    public TenantId TenantId { get; }

    /// <summary>Gets the agent concerned.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the session concerned.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the profile concerned.</summary>
    public MemoryProfileKey ProfileKey { get; }

    /// <summary>Gets the exact profile version.</summary>
    public MemoryProfileVersion ProfileVersion { get; }

    /// <summary>Gets the memory concerned, or <see langword="null"/>.</summary>
    public MemoryId? MemoryId { get; }

    /// <summary>Gets the document concerned, or <see langword="null"/>.</summary>
    public DocumentId? DocumentId { get; }

    /// <summary>Gets the retrieval request concerned, or <see langword="null"/>.</summary>
    public RetrievalRequestId? RetrievalRequestId { get; }

    /// <summary>Gets the bounded outcome name.</summary>
    public string Outcome { get; }

    /// <summary>Gets a bounded count such as the candidates exposed, or <see langword="null"/>.</summary>
    public int? Count { get; }

    /// <summary>Gets the instant of the transition.</summary>
    public DateTimeOffset OccurredAt { get; }
}
