// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A generic <see cref="IDurableOperationCodec{TState}"/> that serializes plain data state as JSON.</summary>
/// <typeparam name="TState">The typed state this codec serializes. It must be plain data with a supported <see cref="JsonSerializer"/> shape.</typeparam>
/// <remarks>
/// <para>
/// Each instance is bound to one exact <see cref="OperationName"/> and <see cref="Version"/> pair and stamps that
/// version's text as the payload's <see cref="OperationPayload.SchemaVersion"/>. A payload recorded under a different
/// schema version is reported as <see cref="DurableDecodeIncompatible{TState}"/> rather than reinterpreted.
/// </para>
/// <para>
/// This codec preserves whatever <typeparamref name="TState"/> itself captures through ordinary
/// <see cref="JsonSerializer"/> semantics, including a declared <c>[JsonExtensionData]</c> property when the type
/// defines one. It does not itself add unknown-field preservation beyond that: a plain property the type does not
/// declare is dropped on decode, the same as any other <see cref="JsonSerializer"/> deserialization. Operations that
/// require guaranteed forward-compatible field retention need a hand-authored codec instead of this generic default.
/// </para>
/// </remarks>
public sealed class JsonDurableOperationCodec<TState>: IDurableOperationCodec<TState>
{
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly int _maximumPayloadBytes;
    private readonly SchemaVersion _schemaVersion;

    /// <summary>Initializes a codec bound to one exact operation name and version.</summary>
    /// <param name="operationName">The non-default operation name this codec serializes state for.</param>
    /// <param name="version">The non-default contract version this codec reads and writes.</param>
    /// <param name="serializerOptions">The JSON options used for encoding and decoding, or <see langword="null"/> to use <see cref="JsonSerializerOptions.Default"/>.</param>
    /// <param name="maximumPayloadBytes">The maximum encoded or recorded UTF-8 byte length this codec accepts, defaulting to 1,048,576 (1 MiB).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationName"/> or <paramref name="version"/> is default, or <paramref name="maximumPayloadBytes"/> is less than one.</exception>
    public JsonDurableOperationCodec(
        DurableOperationName operationName,
        DurableOperationVersion version,
        JsonSerializerOptions? serializerOptions = null,
        int maximumPayloadBytes = 1_048_576)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationName, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumPayloadBytes, 1);
        OperationName = operationName;
        Version = version;
        _serializerOptions = serializerOptions ?? JsonSerializerOptions.Default;
        _maximumPayloadBytes = maximumPayloadBytes;
        _schemaVersion = new SchemaVersion(version.Value);
    }

    /// <inheritdoc/>
    public DurableOperationName OperationName { get; }

    /// <inheritdoc/>
    public DurableOperationVersion Version { get; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="value"/> cannot be represented by <see cref="JsonSerializer"/>, or its encoded form exceeds the configured byte bound.</exception>
    public OperationPayload Encode(TState value)
    {
        byte[] bytes;
        try
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(value, _serializerOptions);
        }
        catch (NotSupportedException exception)
        {
            throw new ArgumentException(
                "The state contains data this codec cannot represent as JSON.", nameof(value), exception);
        }

        return bytes.Length > _maximumPayloadBytes
            ? throw new ArgumentException(
                $"The encoded state occupies {bytes.Length} bytes, exceeding this codec's configured maximum of {_maximumPayloadBytes} bytes.",
                nameof(value))
            : new OperationPayload(_schemaVersion, [.. bytes]);
    }

    /// <inheritdoc/>
    public DurableDecodeResult<TState> Decode(OperationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.SchemaVersion != _schemaVersion)
        {
            return new DurableDecodeIncompatible<TState>(
                payload.SchemaVersion,
                $"This codec reads schema version '{_schemaVersion}', but the payload was recorded under '{payload.SchemaVersion}'.");
        }

        if (payload.Data.Length > _maximumPayloadBytes)
        {
            return new DurableDecodeIncompatible<TState>(
                payload.SchemaVersion,
                $"The payload occupies {payload.Data.Length} bytes, exceeding this codec's configured maximum of {_maximumPayloadBytes} bytes.");
        }

        try
        {
            var state = JsonSerializer.Deserialize<TState>(payload.Data.AsSpan(), _serializerOptions);
            return new DurableDecoded<TState>(state!);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Mirrors Encode's own catch: JsonSerializer also reports an unreadable payload through
            // NotSupportedException (an unsupported TState shape) and InvalidOperationException
            // (serializer/converter misconfiguration), not only JsonException. The interface documents
            // that an unreadable payload returns this incompatible result rather than throwing, so
            // recovery over an existing journal must not crash on any of the three.
            return new DurableDecodeIncompatible<TState>(
                payload.SchemaVersion,
                "The payload could not be parsed as valid JSON for this codec's state type.");
        }
    }
}
