// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Enumerates bounded directory snapshots without conferring file-content or mutation authority.</summary>
public interface IDirectoryReader
{
    /// <summary>Gets the component audience to which enumeration grants must be addressed.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Enumerates one deterministic page after exact lower-boundary grant consumption.</summary>
    /// <param name="request">The authorized page request.</param>
    /// <param name="cancellationToken">Cancels before observation settles.</param>
    /// <returns>The terminal typed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<DirectoryEnumerationResult> EnumerateAsync(
        DirectoryEnumerationRequest request,
        CancellationToken cancellationToken = default);
}
