// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that an exact run-profile publication is unavailable without fallback.</summary>
public sealed record AgentRunProfilePublicationUnavailable: AgentRunProfilePublicationResult
{
    /// <summary>Initializes a caller-safe unavailable result.</summary>
    /// <param name="safeReason">The nonblank content-free reason.</param>
    /// <exception cref="ArgumentNullException"><paramref name="safeReason"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is empty or whitespace.</exception>
    public AgentRunProfilePublicationUnavailable(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the caller-safe unavailable reason.</summary>
    public string SafeReason { get; }
}
