// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Streams file-system change events for one authorized watch operation.</summary>
/// <remarks>Cancellation stops observation and releases implementation-owned watch resources.</remarks>
public interface IFileChangeSource
{
    /// <summary>Observes changes under the authorized watch root.</summary>
    /// <param name="operation">The authorized watch evidence.</param>
    /// <param name="cancellationToken">Stops observation and releases resources when canceled.</param>
    /// <returns>An async sequence of observed changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    public IAsyncEnumerable<FileChange> WatchAsync(
        AuthorizedFileWatch operation,
        CancellationToken cancellationToken = default);
}
