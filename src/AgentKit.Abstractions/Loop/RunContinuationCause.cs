// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed family of typed evidence for one pending reason to continue an open run.</summary>
/// <remarks>
/// A cause records an observation already produced by its owning component. It
/// does not itself admit input, reserve budget, repair output, or authorize a
/// further model request. A policy selects from these immutable observations
/// subject to stop precedence and later session-owner revalidation.
/// </remarks>
public abstract record RunContinuationCause
{
    /// <summary>Initializes the closed cause family from a canonical derived record.</summary>
    /// <remarks>The constructor excludes external cause types so policy implementations can reason over the complete provider-neutral family; the base has no mutable or authority-bearing state.</remarks>
    private protected RunContinuationCause() { }
}
