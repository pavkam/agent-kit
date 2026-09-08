// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Proposes another drive step for an evidence-backed continuation reason.</summary>
/// <remarks>The proposed step remains subject to session-owner revalidation and may be discarded when an affected state, input, or stop observation changes.</remarks>
public sealed record ContinueRun: RunContinuationDecision
{
    /// <summary>Initializes a proposal to continue the open run.</summary>
    /// <param name="reason">The non-null selected cause and retained pending evidence supporting the proposed drive step.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    public ContinueRun(ContinuationReason reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        Reason = reason;
    }

    /// <summary>Gets the evidence-backed reason selected by policy for the proposed drive step.</summary>
    /// <value>A non-null immutable reason that records selection without executing or committing continuation.</value>
    public ContinuationReason Reason { get; }
}
