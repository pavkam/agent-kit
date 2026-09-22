// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one spec
/// <see cref="AuthorizedFileWrite"/> through a capability-bound file writer.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="FileWriteSuccess"/>, <see cref="FileWriteNotFound"/>,
/// <see cref="FileWriteConflict"/>, <see cref="FileWriteDenied"/>,
/// <see cref="FileWriteLimitExceeded"/>, <see cref="FileWriteCancelled"/>,
/// <see cref="FileWriteUnsupported"/>, and <see cref="FileWriteFailed"/>.
/// Its constructor is <see langword="private protected"/>, so no assembly
/// outside AgentKit.Abstractions can extend the hierarchy.
/// </remarks>
public abstract record FileWriteResult
{
    private protected FileWriteResult()
    {
    }
}
