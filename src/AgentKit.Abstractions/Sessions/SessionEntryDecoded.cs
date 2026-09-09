// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a successfully materialized entry and retained original bytes.</summary>
public sealed record SessionEntryDecoded: SessionEntryDecodeResult
{
    /// <summary>Initializes a successful decode result.</summary>
    /// <param name="decoded">The non-null decoded entry and its retained envelope.</param>
    /// <exception cref="ArgumentNullException"><paramref name="decoded"/> is null.</exception>
    public SessionEntryDecoded(DecodedSessionEntry decoded)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        Decoded = decoded;
    }

    /// <summary>Gets the materialized entry and retained original bytes.</summary>
    public DecodedSessionEntry Decoded { get; }
}
