// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Performs bounded content search without conferring general process authority.</summary>
public interface IFileContentSearcher
{
    /// <summary>Gets the component audience to which file-search grants must be addressed.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Executes one exact no-follow search.</summary>
    /// <param name="request">The validated bounded request.</param>
    /// <param name="cancellationToken">Cancels traversal before settlement.</param>
    /// <returns>The typed terminal result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<FileSearchResult> SearchAsync(
        FileSearchRequest request,
        CancellationToken cancellationToken = default);
}
