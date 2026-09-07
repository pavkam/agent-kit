// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Performs bounded in-process glob traversal without conferring file-content or process authority.</summary>
public interface IFileGlobber
{
    /// <summary>Gets the component audience to which glob grants must be addressed.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Traverses and matches one exact authorized glob request.</summary>
    /// <param name="request">The bounded no-follow traversal request.</param>
    /// <param name="cancellationToken">Cancels traversal before settlement.</param>
    /// <returns>The typed terminal result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<GlobResult> GlobAsync(GlobRequest request, CancellationToken cancellationToken = default);
}
