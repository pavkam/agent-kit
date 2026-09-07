// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The input and output limits of one configured
/// <see cref="EmbeddingModelDescriptor"/>, used to clamp or reject requests
/// before they are sent to the provider.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Every limit is nullable because a provider does
/// not always publish an exact figure; a <see langword="null"/> limit means
/// "unknown," never "unlimited," and callers that require a hard bound must
/// treat an unknown limit as a validation gap rather than silently skipping
/// enforcement.
/// </remarks>
public sealed record EmbeddingLimits
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingLimits"/> record.</summary>
    /// <param name="maxInputsPerRequest">The maximum number of inputs the model accepts in one request, when published.</param>
    /// <param name="maxInputTokensPerInput">The maximum token length of a single input, when published.</param>
    /// <param name="defaultDimensions">The model's default output dimensionality, when published.</param>
    /// <param name="maxDimensions">The maximum output dimensionality the model can produce, when published.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any argument is less than one.
    /// </exception>
    public EmbeddingLimits(
        int? maxInputsPerRequest,
        long? maxInputTokensPerInput,
        int? defaultDimensions,
        int? maxDimensions)
    {
        if (maxInputsPerRequest is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInputsPerRequest), maxInputsPerRequest, "Value must be at least one.");
        }

        if (maxInputTokensPerInput is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInputTokensPerInput), maxInputTokensPerInput, "Value must be at least one.");
        }

        if (defaultDimensions is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultDimensions), defaultDimensions, "Value must be at least one.");
        }

        if (maxDimensions is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDimensions), maxDimensions, "Value must be at least one.");
        }

        MaxInputsPerRequest = maxInputsPerRequest;
        MaxInputTokensPerInput = maxInputTokensPerInput;
        DefaultDimensions = defaultDimensions;
        MaxDimensions = maxDimensions;
    }

    /// <summary>Gets the maximum number of inputs the model accepts in one request, when published.</summary>
    public int? MaxInputsPerRequest { get; init; }

    /// <summary>Gets the maximum token length of a single input, when published.</summary>
    public long? MaxInputTokensPerInput { get; init; }

    /// <summary>Gets the model's default output dimensionality, when published.</summary>
    public int? DefaultDimensions { get; init; }

    /// <summary>Gets the maximum output dimensionality the model can produce, when published.</summary>
    public int? MaxDimensions { get; init; }
}
