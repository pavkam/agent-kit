// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One plain-text input to embed.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record TextEmbeddingInput: EmbeddingInput
{
    /// <summary>Initializes a new instance of the <see cref="TextEmbeddingInput"/> record.</summary>
    /// <param name="text">The UTF-8 text to embed.</param>
    /// <param name="correlationId">
    /// An optional caller-supplied correlation identity for this input,
    /// supplementary to its positional index within the request.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public TextEmbeddingInput(string text, EmbeddingInputId? correlationId)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
        CorrelationId = correlationId;
    }

    /// <summary>Gets the UTF-8 text to embed.</summary>
    public string Text { get; init; }

    /// <summary>
    /// Gets an optional caller-supplied correlation identity for this
    /// input, supplementary to its positional index within the request.
    /// </summary>
    public EmbeddingInputId? CorrelationId { get; init; }
}
