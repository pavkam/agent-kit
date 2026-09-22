// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>An owned handle to one received network response and its body stream.</summary>
/// <remarks>
/// The caller owns this handle for as long as it needs the body and must
/// dispose it exactly once; disposing releases or invalidates the
/// underlying connection according to its framing state. Reading beyond
/// the request's configured maximum response size stops the stream with a
/// bounds violation rather than silently truncating or buffering the
/// complete body first.
/// </remarks>
public interface INetworkResponse: IAsyncDisposable
{
    /// <summary>Gets the status code, headers, and declared content length of this response.</summary>
    public NetworkResponseMetadata Metadata { get; }

    /// <summary>Gets evidence of bytes actually sent for the paired request, when available.</summary>
    public NetworkEgressEvidence? EgressEvidence { get; }

    /// <summary>Gets the bounded, readable response body stream.</summary>
    /// <remarks>
    /// Reads throw <see cref="NetworkResponseTooLargeException"/> when actual bytes exceed the configured limit and
    /// <see cref="NetworkResponseTimedOutException"/> when the response deadline expires.
    /// Caller-supplied read cancellation remains <see cref="OperationCanceledException"/>.
    /// </remarks>
    public Stream Content { get; }
}
