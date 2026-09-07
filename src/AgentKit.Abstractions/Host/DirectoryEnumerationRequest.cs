// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one bounded page from a root or child directory under a security grant.</summary>
public sealed record DirectoryEnumerationRequest
{
    /// <summary>Initializes a directory enumeration request.</summary>
    /// <param name="path">The child directory, or null for the configured root.</param>
    /// <param name="maximumEntries">The positive maximum entries retained in this page.</param>
    /// <param name="continuation">The prior snapshot cursor, or null for the first page.</param>
    /// <param name="grant">The bounded authority consumed before directory observation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumEntries"/> is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public DirectoryEnumerationRequest(
        FileSystemPath? path,
        int maximumEntries,
        DirectoryEnumerationCursor? continuation,
        SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        ArgumentNullException.ThrowIfNull(grant);
        Path = path;
        MaximumEntries = maximumEntries;
        Continuation = continuation;
        Grant = grant;
    }

    /// <summary>Gets the child directory, or null for the configured root.</summary>
    public FileSystemPath? Path { get; init; }
    /// <summary>Gets the maximum entries retained in this page.</summary>
    public int MaximumEntries { get; init; }
    /// <summary>Gets the snapshot continuation, when resuming.</summary>
    public DirectoryEnumerationCursor? Continuation { get; init; }
    /// <summary>Gets the bounded authority consumed before observation.</summary>
    public SecurityGrant Grant { get; init; }
}
