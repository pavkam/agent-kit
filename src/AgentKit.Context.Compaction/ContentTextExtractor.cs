// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>
/// Extracts a best-effort, human-readable text rendering of session content,
/// used by the built-in size estimator and extractive strategy.
/// </summary>
/// <remarks>
/// This extractor never interprets or transforms content semantically; it
/// only concatenates the textual portions already present so that
/// deterministic, non-model components have something to measure and quote.
/// Media, reasoning, and structured-data parts contribute no text today;
/// extending extraction to summarize them is a strategy-level concern, not
/// this shared utility's.
/// </remarks>
internal static class ContentTextExtractor
{
    /// <summary>Extracts and concatenates the textual content of one session entry.</summary>
    /// <param name="entry">The entry to extract text from.</param>
    /// <returns>The entry's extracted text, or an empty string if it carries none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public static string ExtractEntryText(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry switch
        {
            MessageSessionEntry message => ExtractPartsText(message.Message.Parts),
            CompactionSessionEntry compaction when compaction.Record.Checkpoint is not null =>
                ExtractPartsText(compaction.Record.Checkpoint.Summary),
            _ => string.Empty
        };
    }

    /// <summary>Extracts and concatenates the textual content of a set of content parts.</summary>
    /// <param name="parts">The parts to extract text from.</param>
    /// <returns>The concatenated text, separated by newlines.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="parts"/> is a default, uninitialized array.
    /// </exception>
    public static string ExtractPartsText(ImmutableArray<ContentPart> parts)
    {
        ArgumentException.ThrowIfDefault(parts);

        var builder = new StringBuilder();
        AppendPartsText(builder, parts);
        return builder.ToString();
    }

    private static void AppendPartsText(StringBuilder builder, ImmutableArray<ContentPart> parts)
    {
        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart text:
                    if (builder.Length > 0)
                    {
                        _ = builder.Append('\n');
                    }

                    _ = builder.Append(text.Text);
                    break;

                case ToolResultPart result:
                    AppendPartsText(builder, result.Content);
                    break;

                default:
                    break;
            }
        }
    }
}
