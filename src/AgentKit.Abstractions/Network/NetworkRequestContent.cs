// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A bounded, in-memory request body.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </para>
/// <para>
/// One-pass upload streams use <see cref="NetworkStagedRequestContent"/> with a
/// separately authorized spool. Response bodies are not affected: <see cref="INetworkResponse"/>
/// always exposes a real, boundedly read stream regardless of how the request
/// body was supplied.
/// </para>
/// </remarks>
public sealed record NetworkRequestContent: INetworkRequestContent
{
    /// <summary>Initializes a new instance of the <see cref="NetworkRequestContent"/> record.</summary>
    /// <param name="contentType">The media type of <paramref name="body"/>.</param>
    /// <param name="body">The request body bytes.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="contentType"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public NetworkRequestContent(string contentType, ReadOnlyMemory<byte> body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ContentType = contentType;
        Body = body;
        BodyFingerprint = ProcessSecurityBinding.FingerprintBytes(body.Span);
    }

    /// <inheritdoc/>
    public string ContentType { get; init; }

    /// <summary>Gets the request body bytes.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <inheritdoc/>
    public ContentHash BodyFingerprint { get; init; }

    /// <inheritdoc/>
    public bool Equals(NetworkRequestContent? other) =>
        other is not null && ContentType == other.ContentType && Body.Span.SequenceEqual(other.Body.Span);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ContentType);
        hash.AddBytes(Body.Span);
        return hash.ToHashCode();
    }
}
