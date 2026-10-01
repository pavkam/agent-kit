// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a policy refused to plan every call it was given.</summary>
public sealed record ToolExecutionPlanRejected: ToolExecutionPlanResult
{
    /// <summary>Initializes a planning rejection.</summary>
    /// <param name="safeReason">A nonblank bounded description safe for terminal results and logs.</param>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is null, empty, or whitespace.</exception>
    public ToolExecutionPlanRejected(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the bounded safe reason.</summary>
    public string SafeReason { get; }
}
