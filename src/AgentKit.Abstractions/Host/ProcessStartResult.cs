// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable base for terminal process start outcomes.</summary>
public abstract record ProcessStartResult
{
    /// <summary>Initializes the base start outcome.</summary>
    private protected ProcessStartResult()
    {
    }
}

/// <summary>A process handle was returned to the caller.</summary>
public sealed record ProcessHandleStarted: ProcessStartResult
{
    /// <summary>Initializes a successful start.</summary>
    /// <param name="handle">The operation-owned handle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handle"/> is null.</exception>
    public ProcessHandleStarted(IProcessHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        Handle = handle;
    }

    /// <summary>Gets the operation-owned handle.</summary>
    public IProcessHandle Handle { get; init; }
}

/// <summary>Start was denied before process creation.</summary>
/// <param name="SafeMessage">The non-sensitive denial message.</param>
public sealed record ProcessStartDenied(string SafeMessage): ProcessStartResult;
