// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports an unknown entry family or schema as opaque retained data.</summary>
public sealed record SessionEntryOpaque: SessionEntryDecodeResult
{
    /// <summary>Initializes an opaque result.</summary>
    /// <param name="wire">The non-null unexecuted envelope retained by the caller or store.</param>
    /// <exception cref="ArgumentNullException"><paramref name="wire"/> is null.</exception>
    public SessionEntryOpaque(SessionEntryWireEnvelope wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        Wire = wire;
    }

    /// <summary>Gets the unexecuted envelope.</summary>
    public SessionEntryWireEnvelope Wire { get; }
}
