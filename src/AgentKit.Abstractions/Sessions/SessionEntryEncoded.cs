// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a successfully encoded immutable envelope.</summary>
public sealed record SessionEntryEncoded: SessionEntryEncodeResult
{
    /// <summary>Initializes a successful encoding result.</summary>
    /// <param name="wire">The non-null encoded envelope.</param>
    /// <exception cref="ArgumentNullException"><paramref name="wire"/> is null.</exception>
    public SessionEntryEncoded(SessionEntryWireEnvelope wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        Wire = wire;
    }

    /// <summary>Gets the encoded envelope.</summary>
    public SessionEntryWireEnvelope Wire { get; }
}
