// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.Options;

/// <summary>
/// A deterministic, non-tokenizer <see cref="ICompactionSizeEstimator"/>
/// that approximates token count from extracted text length.
/// </summary>
/// <remarks>
/// This is a documented, replaceable foundation default: applications with
/// a real tokenizer for their target model register a more accurate
/// <see cref="ICompactionSizeEstimator"/> in its place. This estimator is
/// intentionally simple and fast, trading precision for having no
/// dependency on any specific model's vocabulary.
/// </remarks>
internal sealed class CharacterCompactionSizeEstimator: ICompactionSizeEstimator
{
    private readonly double _charactersPerToken;

    /// <summary>Initializes a new instance of the <see cref="CharacterCompactionSizeEstimator"/> class.</summary>
    /// <param name="options">The validated compaction options carrying the character-per-token ratio.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public CharacterCompactionSizeEstimator(IOptions<CompactionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _charactersPerToken = options.Value.CharactersPerToken;
    }

    /// <inheritdoc/>
    public CompactionSizeEstimate EstimateEntries(ImmutableArray<SessionEntry> entries)
    {
        ArgumentException.ThrowIfDefault(entries);

        long bytes = 0;
        foreach (var entry in entries)
        {
            bytes += Encoding.UTF8.GetByteCount(ContentTextExtractor.ExtractEntryText(entry));
        }

        return new CompactionSizeEstimate(EstimateTokens(bytes), bytes, entries.Length);
    }

    /// <inheritdoc/>
    public CompactionSizeEstimate EstimateCheckpoint(CompactionCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var text = ContentTextExtractor.ExtractPartsText(checkpoint.Summary);
        var bytes = Encoding.UTF8.GetByteCount(text);
        return new CompactionSizeEstimate(EstimateTokens(bytes), bytes, checkpoint.Summary.Length);
    }

    private int EstimateTokens(long bytes) =>
        (int) Math.Ceiling(bytes / Math.Max(_charactersPerToken, 1.0));
}
