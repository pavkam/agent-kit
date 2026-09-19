// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the normalized result of one hook invocation for diagnostics.</summary>
/// <remarks>
/// This classification is deliberately coarse and content-free; it distinguishes how an invocation ended, not why.
/// </remarks>
public enum HookInvocationOutcome
{
    /// <summary>The hook ran to completion and its mutations, if any, passed validation.</summary>
    Succeeded = 0,

    /// <summary>The hook faulted or timed out and the fault was isolated from the owning operation.</summary>
    IsolatedFault,

    /// <summary>The hook faulted or timed out and the fault propagated to the owning operation.</summary>
    Failed,

    /// <summary>The invocation was rejected before execution, for example by a reentrancy limit.</summary>
    Rejected,
}
