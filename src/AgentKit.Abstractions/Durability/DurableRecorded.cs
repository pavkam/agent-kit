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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fencingToken"/> is the default, unallocated token.
    /// </exception>
    public DurableRecorded(FencingToken fencingToken, DateTimeOffset recordedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(fencingToken, default, nameof(fencingToken));
        FencingToken = fencingToken;
        RecordedAt = recordedAt;
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
}
