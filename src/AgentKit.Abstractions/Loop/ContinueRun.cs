// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Proposes another drive step for a supported continuation cause.</summary>
public sealed record ContinueRun: RunContinuationDecision
{
    /// <summary>Initializes a continuation proposal.</summary>
    /// <param name="reason">The selected cause and retained pending evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    public ContinueRun(ContinuationReason reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        Reason = reason;
    }

    /// <summary>Gets the evidence-backed reason selected by policy.</summary>
    public ContinuationReason Reason { get; }
}
