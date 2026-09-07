// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes exact bounded file bytes for deterministic mutation planning.</summary>
public interface IFileSnapshotReader
{
    /// <summary>Gets the component audience for snapshot grants.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Reads one exact authorized file snapshot.</summary>
    /// <param name="request">The bounded snapshot request.</param>
    /// <param name="cancellationToken">Cancels observation.</param>
    /// <returns>The typed terminal result.</returns>
    public ValueTask<FileSnapshotResult> ReadSnapshotAsync(
        FileSnapshotRequest request,
        CancellationToken cancellationToken = default);
}
