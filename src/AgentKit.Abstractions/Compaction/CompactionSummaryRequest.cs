// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A bounded summary-generation request for one compaction attempt.</summary>
public sealed record CompactionSummaryRequest
{
    /// <summary>Initializes a new instance of the <see cref="CompactionSummaryRequest"/> record.</summary>
    /// <param name="context">The compaction operation context.</param>
    /// <param name="generatorKey">The generator key to invoke.</param>
    /// <param name="coveredRange">The covered source range.</param>
    /// <param name="segments">The prepared summary segments.</param>
    /// <param name="maximumOutputTokens">The maximum output tokens allowed.</param>
    /// <param name="effectiveInstructionsFingerprint">The instruction fingerprint observed for the attempt.</param>
    /// <param name="deadline">The attempt deadline.</param>
    /// <param name="extensions">Forward-compatible request data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="segments"/> is default or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A key or token bound is invalid.</exception>
    public CompactionSummaryRequest(
        CompactionOperationContext context,
        CompactionSummaryGeneratorKey generatorKey,
        CompactionSourceRange coveredRange,
        ImmutableArray<CompactionSummarySegment> segments,
        int maximumOutputTokens,
        ContentHash effectiveInstructionsFingerprint,
        DateTimeOffset deadline,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentOutOfRangeException.ThrowIfEqual(generatorKey, default);
        ArgumentException.ThrowIfDefault(segments);
        if (segments.IsEmpty)
        {
            throw new ArgumentException("At least one segment is required.", nameof(segments));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputTokens);
        ArgumentOutOfRangeException.ThrowIfEqual(effectiveInstructionsFingerprint, default);
        Context = context;
        GeneratorKey = generatorKey;
        CoveredRange = coveredRange;
        Segments = segments;
        MaximumOutputTokens = maximumOutputTokens;
        EffectiveInstructionsFingerprint = effectiveInstructionsFingerprint;
        Deadline = deadline;
        Extensions = extensions;
    }

    /// <summary>Gets the compaction operation context.</summary>
    public CompactionOperationContext Context { get; }

    /// <summary>Gets the generator key to invoke.</summary>
    public CompactionSummaryGeneratorKey GeneratorKey { get; }

    /// <summary>Gets the covered source range.</summary>
    public CompactionSourceRange CoveredRange { get; }

    /// <summary>Gets the prepared summary segments.</summary>
    public ImmutableArray<CompactionSummarySegment> Segments { get; }

    /// <summary>Gets the maximum output tokens allowed.</summary>
    public int MaximumOutputTokens { get; }

    /// <summary>Gets the instruction fingerprint observed for the attempt.</summary>
    public ContentHash EffectiveInstructionsFingerprint { get; }

    /// <summary>Gets the attempt deadline.</summary>
    public DateTimeOffset Deadline { get; }

    /// <summary>Gets forward-compatible request data.</summary>
    public ExtensionData Extensions { get; }
}
