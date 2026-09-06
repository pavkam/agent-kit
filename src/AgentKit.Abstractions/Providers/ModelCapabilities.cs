// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares which portable behaviors a configured
/// <see cref="ModelDescriptor"/> supports, so capability negotiation can
/// reject, downgrade, or route around unsupported behavior before any
/// provider request is sent.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately minimal capability inventory covering the
/// behaviors exercised by the current chat-completion request/response
/// contracts. It intentionally omits media, citation, and continuation
/// capabilities that belong to context assembly and structured-output
/// components not yet implemented in this repository; those fields will be
/// added additively once those components exist rather than being
/// fabricated here.
/// </para>
/// </remarks>
public sealed record ModelCapabilities
{
    /// <summary>Initializes a new instance of the <see cref="ModelCapabilities"/> record.</summary>
    /// <param name="supportsSystemInstructions">
    /// Whether the model accepts a dedicated system/developer instruction
    /// role distinct from user content.
    /// </param>
    /// <param name="supportsStreaming">
    /// Whether the model can stream partial response events rather than
    /// only returning a single terminal response.
    /// </param>
    /// <param name="supportsToolCalls">
    /// Whether the model can request application-executed tool calls.
    /// </param>
    /// <param name="supportsParallelToolCalls">
    /// Whether the model can request more than one tool call within a
    /// single response.
    /// </param>
    /// <param name="supportsStructuredOutput">
    /// Whether the model can be constrained to produce output matching a
    /// caller-supplied JSON schema.
    /// </param>
    /// <param name="supportsReasoning">
    /// Whether the model can produce reasoning content or metadata distinct
    /// from its visible response text.
    /// </param>
    /// <param name="supportsVisionInput">
    /// Whether the model accepts image content as part of its input.
    /// </param>
    /// <param name="extensions">Provider-specific capability data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ModelCapabilities(
        bool supportsSystemInstructions,
        bool supportsStreaming,
        bool supportsToolCalls,
        bool supportsParallelToolCalls,
        bool supportsStructuredOutput,
        bool supportsReasoning,
        bool supportsVisionInput,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        SupportsSystemInstructions = supportsSystemInstructions;
        SupportsStreaming = supportsStreaming;
        SupportsToolCalls = supportsToolCalls;
        SupportsParallelToolCalls = supportsParallelToolCalls;
        SupportsStructuredOutput = supportsStructuredOutput;
        SupportsReasoning = supportsReasoning;
        SupportsVisionInput = supportsVisionInput;
        Extensions = extensions;
    }

    /// <summary>
    /// Gets whether the model accepts a dedicated system/developer
    /// instruction role distinct from user content.
    /// </summary>
    public bool SupportsSystemInstructions { get; init; }

    /// <summary>
    /// Gets whether the model can stream partial response events rather
    /// than only returning a single terminal response.
    /// </summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>Gets whether the model can request application-executed tool calls.</summary>
    public bool SupportsToolCalls { get; init; }

    /// <summary>
    /// Gets whether the model can request more than one tool call within a
    /// single response.
    /// </summary>
    public bool SupportsParallelToolCalls { get; init; }

    /// <summary>
    /// Gets whether the model can be constrained to produce output matching
    /// a caller-supplied JSON schema.
    /// </summary>
    public bool SupportsStructuredOutput { get; init; }

    /// <summary>
    /// Gets whether the model can produce reasoning content or metadata
    /// distinct from its visible response text.
    /// </summary>
    public bool SupportsReasoning { get; init; }

    /// <summary>Gets whether the model accepts image content as part of its input.</summary>
    public bool SupportsVisionInput { get; init; }

    /// <summary>Gets provider-specific capability data.</summary>
    public ExtensionData Extensions { get; init; }
}
