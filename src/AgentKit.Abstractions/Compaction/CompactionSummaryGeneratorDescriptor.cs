// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one registered compaction summary generator.</summary>
public sealed record CompactionSummaryGeneratorDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="CompactionSummaryGeneratorDescriptor"/> record.</summary>
    /// <param name="key">The generator key.</param>
    /// <param name="version">The generator version.</param>
    /// <param name="modelBacked">Whether the generator performs model I/O.</param>
    /// <param name="deterministic">Whether repeated requests produce identical summaries.</param>
    /// <param name="maximumInputTokens">The maximum input tokens the generator accepts.</param>
    /// <param name="maximumOutputTokens">The maximum output tokens the generator may produce.</param>
    /// <exception cref="ArgumentOutOfRangeException">A key or version is default, or a token bound is not positive.</exception>
    public CompactionSummaryGeneratorDescriptor(
        CompactionSummaryGeneratorKey key,
        CompactionSummaryGeneratorVersion version,
        bool modelBacked,
        bool deterministic,
        int maximumInputTokens,
        int maximumOutputTokens)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumInputTokens);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputTokens);
        Key = key;
        Version = version;
        ModelBacked = modelBacked;
        Deterministic = deterministic;
        MaximumInputTokens = maximumInputTokens;
        MaximumOutputTokens = maximumOutputTokens;
    }

    /// <summary>Gets the generator key.</summary>
    public CompactionSummaryGeneratorKey Key { get; }

    /// <summary>Gets the generator version.</summary>
    public CompactionSummaryGeneratorVersion Version { get; }

    /// <summary>Gets a value indicating whether the generator performs model I/O.</summary>
    public bool ModelBacked { get; }

    /// <summary>Gets a value indicating whether repeated requests produce identical summaries.</summary>
    public bool Deterministic { get; }

    /// <summary>Gets the maximum input tokens the generator accepts.</summary>
    public int MaximumInputTokens { get; }

    /// <summary>Gets the maximum output tokens the generator may produce.</summary>
    public int MaximumOutputTokens { get; }
}
