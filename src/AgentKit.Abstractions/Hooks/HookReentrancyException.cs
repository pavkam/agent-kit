// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Thrown when a dispatch for one hook point is attempted while a dispatch
/// for that same hook point is already active on the same activation lease,
/// without a bounded reentrancy policy permitting it.
/// </summary>
/// <remarks>
/// AgentKit hooks never use async-local or other ambient state as the
/// authoritative reentrancy mechanism; <see cref="IHookInvocationTracker"/> on the
/// activation lease records active depth per point, and this exception is thrown when
/// entering a point would exceed the configured maximum depth.
/// </remarks>
public sealed class HookReentrancyException: Exception
{
    /// <summary>Initializes a new instance of the <see cref="HookReentrancyException"/> class.</summary>
    /// <param name="message">A message describing the reentrant dispatch that was rejected.</param>
    public HookReentrancyException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HookReentrancyException"/> class.</summary>
    /// <param name="message">A message describing the reentrant dispatch that was rejected.</param>
    /// <param name="innerException">The exception that caused this reentrancy failure, if any.</param>
    public HookReentrancyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HookReentrancyException"/> class.</summary>
    public HookReentrancyException()
    {
    }
}
