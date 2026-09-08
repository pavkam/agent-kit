// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one
/// <see cref="SessionRunLeaseRequest"/>.
/// </summary>
/// <remarks>
/// The built-in kinds are <see cref="SessionRunLeaseAcquired"/>, <see cref="SessionRunBusy"/>,
/// <see cref="SessionRunLeaseConflict"/>, and <see cref="SessionRunLeaseUnavailable"/>.
/// Consumers must fail closed for an unknown derived result.
/// </remarks>
public abstract record SessionRunLeaseResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionRunLeaseResult"/>
    /// record. The <see langword="private protected"/> accessibility limits
    /// ordinary derivation to this assembly; consumers still handle unknown
    /// derived record shapes defensively.
    /// </summary>
    private protected SessionRunLeaseResult()
    {
    }
}
