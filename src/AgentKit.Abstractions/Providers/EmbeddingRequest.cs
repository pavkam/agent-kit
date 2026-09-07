// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The provider-neutral, ready-to-translate content of one embedding
/// request: the inputs to embed and the portable options that shape the
/// resulting vectors.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record EmbeddingRequest
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingRequest"/> record.</summary>
    /// <param name="inputs">The non-empty, ordered inputs to embed.</param>
    /// <param name="purpose">The portable intended use of the resulting embeddings.</param>
    /// <param name="dimensions">
    /// The requested output dimensionality, when the model supports
    /// reducing it; <see langword="null"/> to use the model's default
    /// dimensionality.
    /// </param>
    /// <param name="encoding">
    /// The requested wire encoding of returned vectors, or
    /// <see langword="null"/> to use the model's default encoding.
    /// </param>
    /// <param name="truncation">The policy for handling an overlength input.</param>
    /// <param name="extensions">Provider-specific request data.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="inputs"/> is a default, uninitialized array, is
    /// empty, or contains a null element.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dimensions"/> is less than one.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public EmbeddingRequest(
        ImmutableArray<EmbeddingInput> inputs,
        EmbeddingPurpose purpose,
        int? dimensions,
        EmbeddingEncoding? encoding,
        EmbeddingTruncation truncation,
        ExtensionData extensions)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(inputs);
        ArgumentException.ThrowIfContainsNull(inputs);
        if (dimensions is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), dimensions, "Value must be at least one.");
        }

        ArgumentNullException.ThrowIfNull(extensions);

        Inputs = inputs;
        Purpose = purpose;
        Dimensions = dimensions;
        Encoding = encoding;
        Truncation = truncation;
        Extensions = extensions;
    }

    /// <summary>Gets the non-empty, ordered inputs to embed.</summary>
    public ImmutableArray<EmbeddingInput> Inputs { get; init; }

    /// <summary>Gets the portable intended use of the resulting embeddings.</summary>
    public EmbeddingPurpose Purpose { get; init; }

    /// <summary>
    /// Gets the requested output dimensionality, when the model supports
    /// reducing it; <see langword="null"/> to use the model's default
    /// dimensionality.
    /// </summary>
    public int? Dimensions { get; init; }

    /// <summary>
    /// Gets the requested wire encoding of returned vectors, or
    /// <see langword="null"/> to use the model's default encoding.
    /// </summary>
    public EmbeddingEncoding? Encoding { get; init; }

    /// <summary>Gets the policy for handling an overlength input.</summary>
    public EmbeddingTruncation Truncation { get; init; }

    /// <summary>Gets provider-specific request data.</summary>
    public ExtensionData Extensions { get; init; }

    /// <inheritdoc/>
    public bool Equals(EmbeddingRequest? other) =>
        other is not null
        && Inputs.SequenceEqual(other.Inputs)
        && Purpose == other.Purpose
        && Dimensions == other.Dimensions
        && Encoding == other.Encoding
        && Truncation == other.Truncation
        && Extensions.Equals(other.Extensions);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var input in Inputs)
        {
            hash.Add(input);
        }

        hash.Add(Purpose);
        hash.Add(Dimensions);
        hash.Add(Encoding);
        hash.Add(Truncation);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
