// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Streams the deterministically ordered entries of one authorized directory observation.</summary>
/// <remarks>
/// <para>
/// The implementation consumes the operation's grant once, when enumeration starts, and observes the directory as one
/// bounded snapshot sorted by ordinal entry name, so repeated enumeration of an unchanged directory yields the same
/// sequence. Paging and continuation are a projection over that complete sequence and belong to the consumer.
/// </para>
/// <para>
/// Failures surface from the enumerator rather than as result values, using the standard exception types so the
/// distinction survives any transport: <see cref="UnauthorizedAccessException"/> when the grant is not fresh and exact or
/// the target crosses a symbolic link or inaccessible boundary, <see cref="DirectoryNotFoundException"/> when the directory
/// does not exist, and <see cref="IOException"/> when the listing exceeds the configured snapshot bound or cannot be
/// observed. Messages are safe to show to a model and contain no host paths. Cancellation stops observation and releases
/// enumerator resources owned by the implementation.
/// </para>
/// </remarks>
public interface IDirectoryReader
{
    /// <summary>Gets the security audience that must appear on grants this reader consumes.</summary>
    /// <value>A nondefault component identity matching the host boundary enforcement surface.</value>
    public ComponentId SecurityAudience { get; }

    /// <summary>Enumerates entries under the authorized directory target.</summary>
    /// <param name="operation">The authorized enumeration evidence.</param>
    /// <param name="cancellationToken">Stops observation and releases resources when canceled.</param>
    /// <returns>An async sequence of observed entries ordered by ordinal name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown while enumerating when the grant is denied or the target crosses a boundary.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown while enumerating when the directory does not exist.</exception>
    /// <exception cref="IOException">Thrown while enumerating when the snapshot bound is exceeded or the directory cannot be observed.</exception>
    public IAsyncEnumerable<FileSystemEntry> EnumerateAsync(
        AuthorizedDirectoryEnumeration operation,
        CancellationToken cancellationToken = default);
}
