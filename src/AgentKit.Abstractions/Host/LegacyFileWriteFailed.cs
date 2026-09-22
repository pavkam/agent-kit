// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The legacy <see cref="IFileSystem"/> write could not complete because of an unexpected I/O failure.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
[Obsolete("Use FileWriteFailed from the spec IFileWriter contract instead.")]
public sealed record LegacyFileWriteFailed: LegacyFileWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="LegacyFileWriteFailed"/> record.</summary>
    /// <param name="safeMessage">A human-readable, non-sensitive description of the failure.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public LegacyFileWriteFailed(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a human-readable, non-sensitive description of the failure.</summary>
    public string SafeMessage { get; init; }
}
