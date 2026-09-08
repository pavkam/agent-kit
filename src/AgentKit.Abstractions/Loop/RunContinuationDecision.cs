// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed family of immutable transition proposals returned by an <see cref="IRunContinuationPolicy"/>.</summary>
/// <remarks>
/// A decision proposes continuation, successful or idle completion, or a
/// supported non-success halt. It does not commit a state transition, publish
/// output, prove settlement, or bypass a required stop. The session owner must
/// reacquire its mutation boundary and revalidate the originating context
/// before accepting the proposal.
/// </remarks>
public abstract record RunContinuationDecision
{
    /// <summary>Initializes the closed decision family from a canonical derived record.</summary>
    /// <remarks>The constructor prevents external proposal forms from weakening the continuation contract; derived values remain uncommitted proposals.</remarks>
    private protected RunContinuationDecision() { }
}
