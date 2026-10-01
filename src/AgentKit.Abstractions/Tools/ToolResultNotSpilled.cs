// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no artifact was committed, so the normalizer keeps its bounded fallback.</summary>
public sealed record ToolResultNotSpilled: ToolResultSpillResult
{
    /// <summary>Initializes a refused spill.</summary>
    /// <param name="safeReason">A bounded, content-free explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is null, empty, or whitespace.</exception>
    public ToolResultNotSpilled(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the bounded, content-free explanation.</summary>
    public string SafeReason { get; }
}
