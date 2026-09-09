// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Couples a materialized session entry with its supplied original wire
/// envelope for loss-aware forward-compatible persistence.
/// </summary>
/// <remarks>
/// Codec implementations create this value only after verifying wire identity,
/// schema, limits, and payload coherence. This public value itself performs no
/// authority, provenance, or schema validation; it only preserves the supplied
/// pair for storage and loss-aware replay.
/// </remarks>
public sealed record DecodedSessionEntry
{
    /// <summary>Initializes a successful codec result.</summary>
    /// <param name="entry">The non-null entry materialized by the selected codec.</param>
    /// <param name="wire">The non-null original envelope supplied with the entry.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public DecodedSessionEntry(SessionEntry entry, SessionEntryWireEnvelope wire)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(wire);
        Entry = entry;
        Wire = wire;
    }

    /// <summary>Gets the supplied materialized entry.</summary>
    public SessionEntry Entry { get; }

    /// <summary>Gets the retained wire representation.</summary>
    public SessionEntryWireEnvelope Wire { get; }
}
