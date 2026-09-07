// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// An embedding vector of quantized 8-bit integer elements, one byte per
/// dimension.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. This is distinct from
/// <see cref="PackedBinaryVector"/>, which packs eight dimensions into each
/// byte rather than devoting a full byte to one dimension.
/// </remarks>
public sealed record QuantizedByteVector: EmbeddingVector
{
    /// <summary>Initializes a new instance of the <see cref="QuantizedByteVector"/> record.</summary>
    /// <param name="values">The ordered, non-empty vector elements, one byte per dimension.</param>
    /// <param name="signed">
    /// Whether each element is a signed 8-bit integer (<see langword="true"/>)
    /// or an unsigned 8-bit integer (<see langword="false"/>).
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is a default or empty array.</exception>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifiers should not contain type names",
        Justification = "'signed' is the standard, unambiguous cross-provider wire vocabulary for " +
            "this quantization detail; an invented synonym would only make provider mapping code " +
            "harder to read.")]
    public QuantizedByteVector(ImmutableArray<byte> values, bool signed)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(values);
        Values = values;
        Signed = signed;
    }

    /// <summary>Gets the ordered, non-empty vector elements, one byte per dimension.</summary>
    public ImmutableArray<byte> Values { get; init; }

    /// <summary>
    /// Gets whether each element is a signed 8-bit integer
    /// (<see langword="true"/>) or an unsigned 8-bit integer
    /// (<see langword="false"/>).
    /// </summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifiers should not contain type names",
        Justification = "'Signed' is the standard, unambiguous cross-provider wire vocabulary for " +
            "this quantization detail; an invented synonym would only make provider mapping code " +
            "harder to read.")]
    public bool Signed { get; init; }

    /// <inheritdoc/>
    public bool Equals(QuantizedByteVector? other) =>
        other is not null && Signed == other.Signed && Values.SequenceEqual(other.Values);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Signed);
        foreach (var value in Values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
