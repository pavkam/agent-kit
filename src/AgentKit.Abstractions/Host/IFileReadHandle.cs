// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns one bounded read stream and captured metadata for a single authorized read.</summary>
/// <remarks>The caller must dispose the handle to release implementation resources.</remarks>
public interface IFileReadHandle: IAsyncDisposable
{
    /// <summary>Gets metadata captured when the handle was opened.</summary>
    /// <value>Length and fingerprint evidence for the bounded read.</value>
    public FileMetadata Metadata { get; }

    /// <summary>Gets the bounded content stream.</summary>
    /// <value>A stream that must not expose bytes beyond the authorized bound.</value>
    public Stream Content { get; }
}
