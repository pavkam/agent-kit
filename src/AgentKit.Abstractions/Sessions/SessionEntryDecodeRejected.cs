// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports malformed or limit-exceeding durable data without materializing an entry.</summary>
public sealed record SessionEntryDecodeRejected: SessionEntryDecodeResult
{
    /// <summary>Initializes a rejection with a nonblank content-free reason.</summary>
    /// <param name="reason">The nonblank reason that does not include payload content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty or whitespace.</exception>
    public SessionEntryDecodeRejected(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Reason = reason;
    }

    /// <summary>Gets the content-free reason that decoding was rejected.</summary>
    public string Reason { get; }
}
