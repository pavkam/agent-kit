// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares which portable behaviors a configured
/// <see cref="EmbeddingModelDescriptor"/> supports, so an adapter can
/// reject an unsupported request option before any provider request is
/// sent rather than silently ignoring it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately minimal capability inventory covering the
/// behaviors exercised by the current embedding request/response
/// contracts. It intentionally omits image, audio, and video input
/// capabilities not yet translated by any adapter in this repository; those
/// fields will be added additively once a concrete adapter needs them
/// rather than being fabricated here.
/// </para>
/// </remarks>
public sealed record EmbeddingCapabilities
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingCapabilities"/> record.</summary>
    /// <param name="supportsBatchInput">
    /// Whether the model accepts more than one input in a single request.
    /// </param>
    /// <param name="supportsDimensions">
    /// Whether the model supports requesting a reduced output
    /// dimensionality.
    /// </param>
    /// <param name="supportsPurpose">
    /// Whether the model applies a distinct transform based on the
    /// requested <see cref="EmbeddingPurpose"/>.
    /// </param>
    /// <param name="supportsEncodingSelection">
    /// Whether the model supports requesting a specific
    /// <see cref="EmbeddingEncoding"/> for returned vectors.
    /// </param>
    /// <param name="supportsTruncationControl">
    /// Whether the model supports an explicit, caller-selected
    /// <see cref="EmbeddingTruncation"/> policy rather than always applying
    /// its own fixed behavior.
    /// </param>
    /// <param name="extensions">Provider-specific capability data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public EmbeddingCapabilities(
        bool supportsBatchInput,
        bool supportsDimensions,
        bool supportsPurpose,
        bool supportsEncodingSelection,
        bool supportsTruncationControl,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        SupportsBatchInput = supportsBatchInput;
        SupportsDimensions = supportsDimensions;
        SupportsPurpose = supportsPurpose;
        SupportsEncodingSelection = supportsEncodingSelection;
        SupportsTruncationControl = supportsTruncationControl;
        Extensions = extensions;
    }

    /// <summary>Gets whether the model accepts more than one input in a single request.</summary>
    public bool SupportsBatchInput { get; init; }

    /// <summary>Gets whether the model supports requesting a reduced output dimensionality.</summary>
    public bool SupportsDimensions { get; init; }

    /// <summary>Gets whether the model applies a distinct transform based on the requested <see cref="EmbeddingPurpose"/>.</summary>
    public bool SupportsPurpose { get; init; }

    /// <summary>Gets whether the model supports requesting a specific <see cref="EmbeddingEncoding"/> for returned vectors.</summary>
    public bool SupportsEncodingSelection { get; init; }

    /// <summary>
    /// Gets whether the model supports an explicit, caller-selected
    /// <see cref="EmbeddingTruncation"/> policy rather than always applying
    /// its own fixed behavior.
    /// </summary>
    public bool SupportsTruncationControl { get; init; }

    /// <summary>Gets provider-specific capability data.</summary>
    public ExtensionData Extensions { get; init; }
}
