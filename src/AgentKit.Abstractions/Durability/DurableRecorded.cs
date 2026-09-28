// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The journal write was committed durably under the writer's current
/// ownership generation.
/// </summary>
/// <remarks>
/// This outcome means the record survives process loss. A journal must not
/// return it for a buffered or best-effort write, because the entire recovery
/// model treats a committed record as proof.
/// </remarks>
public sealed record DurableRecorded: DurableRecordResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableRecorded"/>
    /// record.
    /// </summary>
    /// <param name="fencingToken">
    /// The ownership generation the record was committed under.
    /// </param>
    /// <param name="recordedAt">
    /// The instant the journal committed the record, from the injected
    /// <see cref="TimeProvider"/>.
    /// </param>
    /// <param name="enforcement">
    /// Evidence that the write's grant was consumed and its required audit accepted, or
    /// <see langword="null"/> when the journal's selected access contract performs no grant ingress.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fencingToken"/> is the default, unallocated token.
    /// </exception>
    public DurableRecorded(
        FencingToken fencingToken,
        DateTimeOffset recordedAt,
        DurableJournalEnforcementReceipt? enforcement = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(fencingToken, default, nameof(fencingToken));
        FencingToken = fencingToken;
        RecordedAt = recordedAt;
        Enforcement = enforcement;
    }

    /// <summary>
    /// Gets the ownership generation the record was committed under.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, unallocated token.
    /// </exception>
    public FencingToken FencingToken
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(FencingToken));
            field = value;
        }
    }

    /// <summary>Gets the instant the journal committed the record.</summary>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>Gets the write's grant-consumption and audit evidence.</summary>
    /// <value>
    /// The authoritative receipt proving this exact write consumed its single-use grant and completed required
    /// audit, or <see langword="null"/> when the journal performs no grant ingress. A caller that requires
    /// audited durability treats a null receipt as unproven rather than as success.
    /// </value>
    public DurableJournalEnforcementReceipt? Enforcement { get; init; }
}
