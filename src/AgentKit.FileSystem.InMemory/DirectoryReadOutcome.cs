// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

/// <summary>The classified result of observing one directory: the ordered entries, or a safe failure message.</summary>
/// <param name="Status">The terminal classification; observability reports it as the operation outcome.</param>
/// <param name="Entries">The entries ordered by ordinal name on success; empty otherwise.</param>
/// <param name="SafeMessage">A message free of host paths describing a failure; null on success.</param>
internal sealed record DirectoryReadOutcome(DirectoryReadStatus Status, ImmutableArray<FileSystemEntry> Entries, string? SafeMessage)
{
    /// <summary>Creates a successful outcome.</summary>
    /// <param name="entries">The ordered entries.</param>
    /// <returns>The successful outcome.</returns>
    internal static DirectoryReadOutcome Success(ImmutableArray<FileSystemEntry> entries) => new(DirectoryReadStatus.Success, entries, null);

    /// <summary>Creates a failed outcome.</summary>
    /// <param name="status">A status other than <see cref="DirectoryReadStatus.Success"/>.</param>
    /// <param name="message">The safe failure message.</param>
    /// <returns>The failed outcome.</returns>
    internal static DirectoryReadOutcome Failure(DirectoryReadStatus status, string message)
    {
        Debug.Assert(status != DirectoryReadStatus.Success, "A failure outcome must not carry the success status.");
        return new(status, [], message);
    }

    /// <summary>Returns the entries of a successful outcome or throws the <see cref="IDirectoryReader"/> failure exception.</summary>
    /// <returns>The ordered entries.</returns>
    /// <exception cref="UnauthorizedAccessException">The outcome is denied.</exception>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    /// <exception cref="IOException">The snapshot bound was exceeded or the directory could not be observed.</exception>
    internal ImmutableArray<FileSystemEntry> RequireEntries() => Status switch
    {
        DirectoryReadStatus.Success => Entries,
        DirectoryReadStatus.NotFound => throw new DirectoryNotFoundException(SafeMessage),
        DirectoryReadStatus.Denied => throw new UnauthorizedAccessException(SafeMessage),
        DirectoryReadStatus.LimitExceeded or DirectoryReadStatus.Failed => throw new IOException(SafeMessage),
        _ => throw new InvalidOperationException("The directory read status is undefined."),
    };
}
