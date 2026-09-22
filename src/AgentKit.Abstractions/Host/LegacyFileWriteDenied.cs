// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The requested path resolves outside the file system's configured root,
/// exceeds a configured size ceiling, or otherwise fails the low-level
/// legacy <see cref="IFileSystem"/> boundary enforcement.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. This
/// outcome is the low-level implementation refusing the effect itself; it
/// is never produced merely because a higher-level authorization decision
/// was denied before the call reached this boundary.
/// </remarks>
[Obsolete("Use FileWriteDenied from the spec IFileWriter contract instead.")]
public sealed record LegacyFileWriteDenied: LegacyFileWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="LegacyFileWriteDenied"/> record.</summary>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation for the denial.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public LegacyFileWriteDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a human-readable, non-sensitive explanation for the denial.</summary>
    public string SafeMessage { get; init; }
}
