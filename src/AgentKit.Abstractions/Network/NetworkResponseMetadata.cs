// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The status code, headers, and declared content length of one received response.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. A
/// declared <see cref="ContentLength"/> is a hint only; the transport still
/// enforces the request's configured maximum response size against bytes
/// actually read from <see cref="INetworkResponse.Content"/>.
/// </remarks>
public sealed record NetworkResponseMetadata
{
    /// <summary>Initializes a new instance of the <see cref="NetworkResponseMetadata"/> record.</summary>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="headers">The response headers.</param>
    /// <param name="contentLength">The declared content length, when the response supplied one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is null.</exception>
    public NetworkResponseMetadata(int statusCode, NetworkHeaderSet headers, long? contentLength)
    {
        ArgumentNullException.ThrowIfNull(headers);

        StatusCode = statusCode;
        Headers = headers;
        ContentLength = contentLength;
    }

    /// <summary>Gets the response status code.</summary>
    public int StatusCode { get; init; }

    /// <summary>Gets the response headers.</summary>
    public NetworkHeaderSet Headers { get; init; }

    /// <summary>Gets the declared content length, when the response supplied one.</summary>
    public long? ContentLength { get; init; }
}
