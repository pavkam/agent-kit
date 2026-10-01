// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Durable session fact that one tool call reached its authoritative terminal outcome.</summary>
/// <remarks>
/// <para>
/// The entry persists terminal <em>evidence</em>: status, side-effect certainty, retryability, safe error, acceptance
/// and grant correlation, normalization provenance, policy references, and timing. It deliberately holds a
/// <see cref="ToolCallResult"/> whose <see cref="ToolCallResult.Content"/> is empty: normalized result content is
/// committed once, in its bounded projected form, by the tool message the loop appends, so the session never stores a
/// second unbounded copy. A terminal record for a call that was accepted lets recovery distinguish a settled call from one
/// whose effect may have happened.
/// </para>
/// <para>Rejected-before-acceptance calls record a terminal entry too, so every identified call has exactly one.</para>
/// </remarks>
public sealed record ToolCallTerminalSessionEntry: SessionEntry
{
    /// <summary>Initializes a terminal-outcome session fact.</summary>
    /// <param name="id">The stable entry identity.</param>
    /// <param name="address">The owning session, which must equal the result's agent and session.</param>
    /// <param name="correlation">The result's in-run correlation, which must equal its authorization scope correlation.</param>
    /// <param name="branchId">The selected branch.</param>
    /// <param name="sequence">The branch-local commit sequence.</param>
    /// <param name="causalParentId">The accepted-call entry when the call was accepted, otherwise the entry that requested it.</param>
    /// <param name="recordedAt">The commit time from the injected clock.</param>
    /// <param name="schemaVersion">The durable schema version.</param>
    /// <param name="result">The nonnull terminal evidence whose content is empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/>, <paramref name="correlation"/>, or <paramref name="result"/> is null.</exception>
    /// <exception cref="ArgumentException">The address or correlation does not match the result's scope, or the result carries content.</exception>
    public ToolCallTerminalSessionEntry(
        SessionEntryId id,
        SessionAddress address,
        InRunOperationCorrelation correlation,
        BranchId branchId,
        SessionSequence sequence,
        SessionEntryId? causalParentId,
        DateTimeOffset recordedAt,
        SchemaVersion schemaVersion,
        ToolCallResult result)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNotEqual(address, new SessionAddress(result.AgentId, result.SessionId), nameof(address));
        ArgumentException.ThrowIfNotEqual(correlation, result.Authorization.Scope.Correlation, nameof(correlation));
        ArgumentException.ThrowIfNotEqual(result.Content.IsEmpty, true, nameof(result));
        Result = result;
    }

    /// <summary>Gets the terminal evidence.</summary>
    /// <value>A terminal record whose content is empty by construction.</value>
    public ToolCallResult Result { get; init; }
}
