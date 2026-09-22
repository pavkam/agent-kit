// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one legacy
/// <see cref="FileWriteRequest"/> through <see cref="IFileSystem"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="LegacyFileWritten"/>, <see cref="LegacyFileAlreadyExists"/>,
/// <see cref="LegacyFileWriteDenied"/>, and <see cref="LegacyFileWriteFailed"/>.
/// Its constructor is <see langword="private protected"/>, so no assembly
/// outside AgentKit.Abstractions can add a fifth kind.
/// </remarks>
[Obsolete("Use IFileWriter with the spec FileWriteResult hierarchy instead.")]
public abstract record LegacyFileWriteResult
{
    private protected LegacyFileWriteResult()
    {
    }
}
