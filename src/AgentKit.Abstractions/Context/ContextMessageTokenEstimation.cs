// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Text.Json;

/// <summary>Deterministic, non-tokenizer message token estimation shared by context and loop pressure checks.</summary>
public static class ContextMessageTokenEstimation
{
    /// <summary>Estimates tokens from UTF-16 text-bearing message parts.</summary>
    /// <param name="messages">The messages to measure.</param>
    /// <param name="estimatedCharactersPerToken">The positive character-per-token ratio.</param>
    /// <returns>An advisory estimate; never used as the sole authorization gate.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="estimatedCharactersPerToken"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="messages"/> is a default, uninitialized array.</exception>
    public static long EstimateTokens(ImmutableArray<AgentMessage> messages, double estimatedCharactersPerToken)
    {
        ArgumentException.ThrowIfDefault(messages);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(estimatedCharactersPerToken);

        long characters = 0;
        foreach (var message in messages)
        {
            foreach (var part in message.Parts)
            {
                characters += EstimatePartCharacters(part);
            }
        }

        return (long) Math.Ceiling(characters / estimatedCharactersPerToken);
    }

    /// <summary>Estimates tokens from UTF-16 text-bearing content parts.</summary>
    /// <param name="parts">The parts to measure.</param>
    /// <param name="estimatedCharactersPerToken">The positive character-per-token ratio.</param>
    /// <returns>An advisory estimate; never used as the sole authorization gate.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="estimatedCharactersPerToken"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="parts"/> is a default, uninitialized array.</exception>
    public static long EstimateTokens(ImmutableArray<ContentPart> parts, double estimatedCharactersPerToken)
    {
        ArgumentException.ThrowIfDefault(parts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(estimatedCharactersPerToken);

        long characters = 0;
        foreach (var part in parts)
        {
            characters += EstimatePartCharacters(part);
        }

        return (long) Math.Ceiling(characters / estimatedCharactersPerToken);
    }

    private static long EstimatePartCharacters(ContentPart part) => part switch
    {
        TextPart text => text.Text.Length,
        ToolCallPart call => call.Arguments.ValueKind == JsonValueKind.Undefined ? 0 : call.Arguments.GetRawText().Length,
        ToolResultPart result => result.Content.OfType<TextPart>().Sum(static inner => (long) inner.Text.Length),
        ReasoningPart { Content.Text: { } reasoning } => reasoning.Length,
        _ => 0,
    };
}
