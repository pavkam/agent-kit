// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one
/// <see cref="FileWriteRequest"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="FileWritten"/>, <see cref="FileAlreadyExists"/>,
/// <see cref="FileWriteDenied"/>, and <see cref="FileWriteFailed"/>. Its
/// constructor is <see langword="private protected"/>, so no assembly
/// outside AgentKit.Abstractions can add a fifth kind.
/// </remarks>
public abstract record FileWriteResult
{
    private protected FileWriteResult()
    {
    }
}
