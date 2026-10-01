// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable base for the terminal outcome of one <see cref="AuthorizedFileDelete"/> through a capability-bound file deleter.</summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are <see cref="FileDeleteSuccess"/>, <see cref="FileDeleteNotFound"/>,
/// <see cref="FileDeleteConflict"/>, <see cref="FileDeleteDenied"/>, <see cref="FileDeleteCancelled"/>,
/// <see cref="FileDeleteUnsupported"/>, and <see cref="FileDeleteFailed"/>. Its constructor is <see langword="private protected"/>, so
/// no assembly outside AgentKit.Abstractions can extend the hierarchy.
/// </remarks>
public abstract record FileDeleteResult
{
    private protected FileDeleteResult()
    {
    }
}
