// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A one-pass request body whose full payload fingerprint was authorized after
/// bounded staging.
/// </summary>
public sealed record NetworkStagedRequestContent: INetworkRequestContent
{
    /// <summary>Initializes staged request content.</summary>
    /// <param name="contentType">The media type of the staged body.</param>
    /// <param name="bodyFingerprint">The exact authorized payload fingerprint.</param>
    /// <param name="spool">The spool that exposes staged bytes once per send.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="contentType"/> is null, empty, or whitespace, or <paramref name="bodyFingerprint"/> is default.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="spool"/> is null.</exception>
    public NetworkStagedRequestContent(
        string contentType,
        ContentHash bodyFingerprint,
        INetworkRequestBodySpool spool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfEqual(bodyFingerprint, default);
        ArgumentNullException.ThrowIfNull(spool);
        ContentType = contentType;
        BodyFingerprint = bodyFingerprint;
        Spool = spool;
    }

    /// <inheritdoc/>
    public string ContentType { get; init; }

    /// <inheritdoc/>
    public ContentHash BodyFingerprint { get; init; }

    /// <summary>Gets the spool that exposes staged bytes once per send.</summary>
    public INetworkRequestBodySpool Spool { get; init; }
}
