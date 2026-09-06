// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of loading one session's
/// current descriptor.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="SessionLoaded"/>, <see cref="SessionNotFound"/>, and
/// <see cref="SessionLoadFailed"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a fourth kind.
/// </remarks>
public abstract record SessionLoadResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionLoadResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected SessionLoadResult()
    {
    }
}
