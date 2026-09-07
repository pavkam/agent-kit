// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The element representation of a decoded <see cref="EmbeddingVector"/>,
/// carried on <see cref="EmbeddingSpaceIdentity"/> because two vectors are
/// comparable only when their element interpretation matches.
/// </summary>
public enum EmbeddingElementType
{
    /// <summary>32-bit floating-point elements, one per dimension.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifiers should not contain type names",
        Justification = "'Float32' is the standard, unambiguous cross-provider wire vocabulary for " +
            "this element width; an invented synonym would only make provider mapping code harder " +
            "to read.")]
    Float32,

    /// <summary>64-bit floating-point elements, one per dimension.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifiers should not contain type names",
        Justification = "'Float64' is the standard, unambiguous cross-provider wire vocabulary for " +
            "this element width; an invented synonym would only make provider mapping code harder " +
            "to read.")]
    Float64,

    /// <summary>Signed 8-bit integer quantized elements, one per dimension.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifiers should not contain type names",
        Justification = "'Int8' is the standard, unambiguous cross-provider wire vocabulary for this " +
            "quantized element width; an invented synonym would only make provider mapping code " +
            "harder to read.")]
    Int8,

    /// <summary>Unsigned 8-bit integer quantized elements, one per dimension.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifiers should not contain type names",
        Justification = "'UInt8' is the standard, unambiguous cross-provider wire vocabulary for this " +
            "quantized element width; an invented synonym would only make provider mapping code " +
            "harder to read.")]
    UInt8,

    /// <summary>Signed, bit-packed binary elements, one bit per dimension.</summary>
    Binary,

    /// <summary>Unsigned, bit-packed binary elements, one bit per dimension.</summary>
    UBinary,
}
