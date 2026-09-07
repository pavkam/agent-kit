// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The requested wire encoding of returned embedding vectors. Not every
/// value is supported by every model; an adapter rejects a requested
/// encoding its configured model does not support rather than silently
/// substituting a different one.
/// </summary>
/// <remarks>
/// This describes the requested wire representation, not the resulting
/// <see cref="EmbeddingVector"/> element type. A response is always decoded
/// into a fully typed <see cref="EmbeddingVector"/> before it reaches
/// application code; a base64-encoded response body, for example, is
/// decoded into a <see cref="DenseFloatVector"/> or
/// <see cref="QuantizedByteVector"/> rather than surfaced as an opaque
/// string, since the encoding is a wire detail and not itself a distinct
/// embedding space.
/// </remarks>
public enum EmbeddingEncoding
{
    /// <summary>Standard floating-point vector elements.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifiers should not contain type names",
        Justification = "'Float' is the standard, unambiguous cross-provider wire vocabulary for this " +
            "encoding; an invented synonym would only make provider mapping code harder to read.")]
    Float,

    /// <summary>Signed 8-bit integer quantized vector elements.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifiers should not contain type names",
        Justification = "'Int8' is the standard, unambiguous cross-provider wire vocabulary for this " +
            "encoding; an invented synonym would only make provider mapping code harder to read.")]
    Int8,

    /// <summary>Unsigned 8-bit integer quantized vector elements.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifiers should not contain type names",
        Justification = "'UInt8' is the standard, unambiguous cross-provider wire vocabulary for this " +
            "encoding; an invented synonym would only make provider mapping code harder to read.")]
    UInt8,

    /// <summary>Signed, bit-packed binary vector elements (one bit per dimension).</summary>
    Binary,

    /// <summary>Unsigned, bit-packed binary vector elements (one bit per dimension).</summary>
    UBinary,
}
