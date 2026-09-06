// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for one fact appended to a session's ordered,
/// append-only record.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="AgentMessage"/> and <see cref="ContentPart"/>, this
/// hierarchy is intentionally <em>open</em>: its constructor is
/// <see langword="protected"/>, not <see langword="private protected"/>,
/// because the components that own each fact family — messages, input
/// admission, tool calls, compaction, security decisions, goals, recovery
/// checkpoints — live in different packages and each need to add their own
/// concrete entry kind. <see cref="MessageSessionEntry"/> is the one
/// concrete kind defined in this package; every other package that appends
/// to a session record defines its own <see cref="SessionEntry"/> subtype
/// rather than smuggling its payload into <see cref="ExtensionData"/> on an
/// existing kind.
/// </para>
/// <para>
/// Every entry carries stable identity, monotonic sequence, optional causal
/// parent, and a timestamp from the injected <see cref="TimeProvider"/>, so
/// ordering and causality survive storage, branching, and replay without
/// relying on array position.
/// </para>
/// </remarks>
public abstract record SessionEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionEntry"/> record.
    /// </summary>
    /// <param name="id">The stable identity of this entry.</param>
    /// <param name="address">The session this entry belongs to.</param>
    /// <param name="correlation">The causal operation that produced this entry.</param>
    /// <param name="branchId">The branch this entry belongs to.</param>
    /// <param name="sequence">This entry's position within its branch.</param>
    /// <param name="causalParentId">
    /// The entry this one causally follows, when applicable (for example, a
    /// tool result's causal parent is its tool call).
    /// </param>
    /// <param name="recordedAt">
    /// The commit time from the injected <see cref="TimeProvider"/>.
    /// </param>
    /// <param name="schemaVersion">The durable schema version of this entry.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> or <paramref name="correlation"/> is null.
    /// </exception>
    protected SessionEntry(
        SessionEntryId id,
        SessionAddress address,
        OperationCorrelation correlation,
        BranchId branchId,
        SessionSequence sequence,
        SessionEntryId? causalParentId,
        DateTimeOffset recordedAt,
        SchemaVersion schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(correlation);

        Id = id;
        Address = address;
        Correlation = correlation;
        BranchId = branchId;
        Sequence = sequence;
        CausalParentId = causalParentId;
        RecordedAt = recordedAt;
        SchemaVersion = schemaVersion;
    }

    /// <summary>Gets the stable identity of this entry.</summary>
    public SessionEntryId Id { get; init; }

    /// <summary>Gets the session this entry belongs to.</summary>
    public SessionAddress Address { get; init; }

    /// <summary>Gets the causal operation that produced this entry.</summary>
    public OperationCorrelation Correlation { get; init; }

    /// <summary>Gets the branch this entry belongs to.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets this entry's position within its branch.</summary>
    public SessionSequence Sequence { get; init; }

    /// <summary>
    /// Gets the entry this one causally follows, when applicable.
    /// </summary>
    public SessionEntryId? CausalParentId { get; init; }

    /// <summary>
    /// Gets the commit time from the injected <see cref="TimeProvider"/>.
    /// </summary>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>Gets the durable schema version of this entry.</summary>
    public SchemaVersion SchemaVersion { get; init; }
}
