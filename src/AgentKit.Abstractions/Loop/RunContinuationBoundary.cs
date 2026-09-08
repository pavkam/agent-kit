// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed family of safe-boundary evidence shapes used when evaluating continuation.</summary>
/// <remarks>
/// A boundary says whether evaluation follows a committed turn, an existing
/// retryable request, a resolved deferred operation, or idle state. It carries
/// only immutable correlation evidence and does not establish that referenced
/// records committed. The session owner establishes and revalidates that fact.
/// </remarks>
public abstract record RunContinuationBoundary
{
    /// <summary>Initializes the closed boundary family from a canonical derived record.</summary>
    /// <remarks>The constructor is inaccessible to external assemblies so all boundary shapes remain known to the loop contract; the base owns no mutable state.</remarks>
    private protected RunContinuationBoundary() { }
}
