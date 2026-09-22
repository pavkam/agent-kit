// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns one temporary file and its bounded write stream until disposal.</summary>
/// <remarks>The caller must dispose the lease to release host resources and finalize temporary file lifetime.</remarks>
public interface ITemporaryFileLease: IAsyncDisposable
{
    /// <summary>Gets the resolved temporary file target.</summary>
    /// <value>The host-bound path evidence for the leased file.</value>
    public ResolvedFileTarget Target { get; }

    /// <summary>Gets the bounded content stream for the temporary file.</summary>
    /// <value>A stream that must not expose bytes beyond the authorized bound.</value>
    public Stream Content { get; }
}
