// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The token limits of one configured <see cref="ModelDescriptor"/>, used to
/// clamp or reject requests before they are sent to the provider.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Both limits are nullable because a provider does
/// not always publish an exact figure; a <see langword="null"/> limit means
/// "unknown," never "unlimited," and callers that require a hard bound must
/// treat an unknown limit as a validation gap rather than silently skipping
/// enforcement.
/// </remarks>
public sealed record ModelLimits
{
    /// <summary>Initializes a new instance of the <see cref="ModelLimits"/> record.</summary>
    /// <param name="maxContextTokens">
    /// The maximum combined input and output tokens the model's context
    /// window can hold, when published.
    /// </param>
    /// <param name="maxOutputTokens">
    /// The maximum number of output tokens the model can produce in a
    /// single response, when published.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxContextTokens"/> or <paramref name="maxOutputTokens"/>
    /// is negative.
    /// </exception>
    public ModelLimits(long? maxContextTokens, long? maxOutputTokens)
    {
        if (maxContextTokens is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxContextTokens),
                maxContextTokens,
                "Value must not be negative.");
        }

        if (maxOutputTokens is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOutputTokens),
                maxOutputTokens,
                "Value must not be negative.");
        }

        MaxContextTokens = maxContextTokens;
        MaxOutputTokens = maxOutputTokens;
    }

    /// <summary>
    /// Gets the maximum combined input and output tokens the model's
    /// context window can hold, when published.
    /// </summary>
    public long? MaxContextTokens { get; init; }

    /// <summary>
    /// Gets the maximum number of output tokens the model can produce in a
    /// single response, when published.
    /// </summary>
    public long? MaxOutputTokens { get; init; }
}
