// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A session entry carrying one durable <see cref="AgentMessage"/>.
/// </summary>
/// <remarks>
/// This is the primary entry kind: every user, assistant, system,
/// developer, tool, and runtime message a session records is wrapped in one
/// of these. The entry's own <see cref="SessionEntry.Id"/>,
/// <see cref="SessionEntry.BranchId"/>, and <see cref="SessionEntry.Sequence"/>
/// are the authoritative ordering facts; <see cref="Message"/> carries the
/// conversational content itself.
/// </remarks>
public sealed record MessageSessionEntry: SessionEntry
{
    /// <summary>Initializes a new instance of the <see cref="MessageSessionEntry"/> record.</summary>
    /// <param name="id">The stable identity of this entry.</param>
    /// <param name="address">The session this entry belongs to.</param>
    /// <param name="correlation">The causal operation that produced this entry.</param>
    /// <param name="branchId">The branch this entry belongs to.</param>
    /// <param name="sequence">This entry's position within its branch.</param>
    /// <param name="causalParentId">The entry this one causally follows, when applicable.</param>
    /// <param name="recordedAt">The commit time from the injected <see cref="TimeProvider"/>.</param>
    /// <param name="schemaVersion">The durable schema version of this entry.</param>
    /// <param name="message">The durable message carried by this entry.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> (or a base parameter) is null.
    /// </exception>
    public MessageSessionEntry(
        SessionEntryId id,
        SessionAddress address,
        OperationCorrelation correlation,
        BranchId branchId,
        SessionSequence sequence,
        SessionEntryId? causalParentId,
        DateTimeOffset recordedAt,
        SchemaVersion schemaVersion,
        AgentMessage message)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(message);
        Message = message;
    }

    /// <summary>Gets the durable message carried by this entry.</summary>
    public AgentMessage Message { get; init; }
}
