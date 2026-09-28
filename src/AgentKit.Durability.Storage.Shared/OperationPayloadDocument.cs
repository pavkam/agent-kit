// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of <see cref="OperationPayload"/>, one versioned opaque durable state payload.</summary>
/// <remarks>
/// The bytes are persisted as standard base64 rather than a JSON number array so an operation's state occupies a
/// predictable, compact line and survives any text-oriented inspection of the store unchanged. An empty payload is
/// valid and round-trips as an empty string, which is distinct from an absent payload.
/// </remarks>
/// <param name="SchemaVersion">The non-blank schema version the bytes were written under.</param>
/// <param name="Data">The base64 encoding of the serialized payload bytes; empty for a payload carrying no data.</param>
internal sealed record OperationPayloadDocument(string SchemaVersion, string Data)
{
    /// <summary>Projects one domain payload into its portable persisted representation.</summary>
    /// <param name="value">The non-null payload to project.</param>
    /// <returns>A document carrying the unwrapped schema version and base64 bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static OperationPayloadDocument FromDomain(OperationPayload value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new OperationPayloadDocument(
            value.SchemaVersion.Value,
            Convert.ToBase64String(value.Data.AsSpan()));
    }

    /// <summary>Reconstructs the exact domain payload this document was projected from.</summary>
    /// <returns>A payload equal to the projected original, including its exact byte sequence.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Data"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="SchemaVersion"/> is blank.</exception>
    /// <exception cref="FormatException"><see cref="Data"/> is not valid base64.</exception>
    internal OperationPayload ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Data);
        return new OperationPayload(new SchemaVersion(SchemaVersion), [.. Convert.FromBase64String(Data)]);
    }
}
