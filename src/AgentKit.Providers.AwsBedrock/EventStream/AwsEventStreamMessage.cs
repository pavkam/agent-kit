// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.EventStream;

/// <summary>
/// One decoded AWS event stream message: its headers and raw payload
/// bytes.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
internal sealed record AwsEventStreamMessage
{
    /// <summary>Initializes a new instance of the <see cref="AwsEventStreamMessage"/> record.</summary>
    /// <param name="headers">The decoded message headers, keyed by header name.</param>
    /// <param name="payload">The raw payload bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> or <paramref name="payload"/> is null.</exception>
    public AwsEventStreamMessage(IReadOnlyDictionary<string, string> headers, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(payload);

        Headers = headers;
        Payload = payload;
    }

    /// <summary>Gets the decoded message headers, keyed by header name.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; }

    /// <summary>Gets the raw payload bytes.</summary>
    public byte[] Payload { get; init; }

    /// <summary>Gets the <c>:message-type</c> header value, when present.</summary>
    public string? MessageType => Headers.GetValueOrDefault(":message-type");

    /// <summary>Gets the <c>:event-type</c> header value, when present.</summary>
    public string? EventType => Headers.GetValueOrDefault(":event-type");

    /// <summary>Gets the <c>:exception-type</c> header value, when present.</summary>
    public string? ExceptionType => Headers.GetValueOrDefault(":exception-type");

    /// <summary>Gets the <c>:error-code</c> header value, when present.</summary>
    /// <remarks>
    /// Present on a <c>:message-type: error</c> frame instead of an
    /// <c>:exception-type</c> header. The event-stream <c>error</c> message
    /// type typically carries no JSON payload at all, unlike <c>exception</c>.
    /// </remarks>
    public string? ErrorCode => Headers.GetValueOrDefault(":error-code");

    /// <summary>Gets the <c>:error-message</c> header value, when present.</summary>
    /// <remarks>Present on a <c>:message-type: error</c> frame instead of a JSON body <c>message</c> field.</remarks>
    public string? ErrorMessage => Headers.GetValueOrDefault(":error-message");
}
