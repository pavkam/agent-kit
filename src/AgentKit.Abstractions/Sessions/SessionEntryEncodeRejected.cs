// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that an entry cannot be represented without unsafe loss.</summary>
public sealed record SessionEntryEncodeRejected: SessionEntryEncodeResult
{
    /// <summary>Initializes a rejection with a nonblank content-free reason.</summary>
    /// <param name="reason">The nonblank reason that does not include entry content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty or whitespace.</exception>
    public SessionEntryEncodeRejected(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Reason = reason;
    }

    /// <summary>Gets the content-free reason that encoding was rejected.</summary>
    public string Reason { get; }
}
