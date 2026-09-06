// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one
/// <see cref="SessionReadRequest"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="SessionPage"/>, <see cref="SessionReadNotFound"/>, and
/// <see cref="SessionReadFailed"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a fourth kind.
/// </remarks>
public abstract record SessionPageResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionPageResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected SessionPageResult()
    {
    }
}
