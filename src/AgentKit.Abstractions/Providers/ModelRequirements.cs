// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The portable behaviors one request actually needs, stated before a model
/// is chosen so that an incompatible model is rejected rather than silently
/// downgraded.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Every flag means "this request will not work correctly without it". A
/// requirement is not a preference: leaving a flag <see langword="false"/>
/// means the request tolerates a model lacking that behavior, not that the
/// behavior is unwanted.
/// </para>
/// </remarks>
public sealed record ModelRequirements
{
    /// <summary>
    /// A requirements value that accepts any configured conversational model.
    /// </summary>
    /// <value>
    /// Useful for plain text completion where no optional behavior is needed.
    /// </value>
    public static ModelRequirements None { get; } = new();

    /// <summary>
    /// Gets whether the request needs a dedicated system or developer
    /// instruction role distinct from user content.
    /// </summary>
    public bool RequiresSystemInstructions { get; init; }

    /// <summary>
    /// Gets whether the request needs incremental response streaming.
    /// </summary>
    public bool RequiresStreaming { get; init; }

    /// <summary>Gets whether the request needs model-requested tool calls.</summary>
    public bool RequiresToolCalls { get; init; }

    /// <summary>
    /// Gets whether the request needs several tool calls requested in one
    /// response.
    /// </summary>
    /// <value>
    /// Implies <see cref="RequiresToolCalls"/> semantically; validation
    /// reports both when a model supports neither.
    /// </value>
    public bool RequiresParallelToolCalls { get; init; }

    /// <summary>
    /// Gets whether the request needs provider-enforced structured output.
    /// </summary>
    public bool RequiresStructuredOutput { get; init; }

    /// <summary>Gets whether the request needs reasoning support.</summary>
    public bool RequiresReasoning { get; init; }

    /// <summary>Gets whether the request needs image input.</summary>
    public bool RequiresVisionInput { get; init; }

    /// <summary>
    /// Gets the minimum input context window the request needs, or
    /// <see langword="null"/> when the caller has not estimated one.
    /// </summary>
    /// <value>
    /// A positive token count when specified. <see langword="null"/> means
    /// unknown, which is treated as "no constraint" rather than as zero.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer supplies a value that is zero or negative.
    /// </exception>
    public long? MinimumInputTokens
    {
        get;
        init
        {
            if (value is { } tokens)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                    tokens,
                    nameof(MinimumInputTokens));
            }

            field = value;
        }
    }
}
