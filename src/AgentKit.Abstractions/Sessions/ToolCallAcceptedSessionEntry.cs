// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Durable session fact that one authorized tool call was accepted for invocation.</summary>
/// <remarks>
/// <para>
/// The entry is appended through the run's session capability before any invoker starts, so recovery can tell a call
/// that never started from one whose effect may have happened. It holds the complete <see cref="AcceptedToolCall"/>:
/// resolved identity, declared effects, idempotency key, admission and acceptance evidence, and the captured
/// normalization and projection policies. It carries no arguments and no result content.
/// </para>
/// <para>
/// It is historical evidence, not authority. It never reauthorizes, never mints or consumes a grant, and is never a
/// message: history assembly ignores it.
/// </para>
/// </remarks>
public sealed record ToolCallAcceptedSessionEntry: SessionEntry
{
    /// <summary>Initializes an accepted-call session fact.</summary>
    /// <param name="id">The stable entry identity.</param>
    /// <param name="address">The owning session, which must equal the call's agent and session.</param>
    /// <param name="correlation">The call's in-run correlation, which must equal the call's authorization scope correlation.</param>
    /// <param name="branchId">The selected branch.</param>
    /// <param name="sequence">The branch-local commit sequence.</param>
    /// <param name="causalParentId">The entry this one causally follows, normally the assistant message that requested the call.</param>
    /// <param name="recordedAt">The commit time from the injected clock.</param>
    /// <param name="schemaVersion">The durable schema version.</param>
    /// <param name="call">The nonnull accepted-call evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/>, <paramref name="correlation"/>, or <paramref name="call"/> is null.</exception>
    /// <exception cref="ArgumentException">The address or correlation does not match the call's authorization scope.</exception>
    public ToolCallAcceptedSessionEntry(
        SessionEntryId id,
        SessionAddress address,
        InRunOperationCorrelation correlation,
        BranchId branchId,
        SessionSequence sequence,
        SessionEntryId? causalParentId,
        DateTimeOffset recordedAt,
        SchemaVersion schemaVersion,
        AcceptedToolCall call)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentException.ThrowIfNotEqual(address, new SessionAddress(call.AgentId, call.SessionId), nameof(address));
        ArgumentException.ThrowIfNotEqual(correlation, call.Authorization.Scope.Correlation, nameof(correlation));
        Call = call;
    }

    /// <summary>Gets the accepted-call evidence.</summary>
    /// <value>The immutable record committed before invocation.</value>
    public AcceptedToolCall Call { get; init; }
}
