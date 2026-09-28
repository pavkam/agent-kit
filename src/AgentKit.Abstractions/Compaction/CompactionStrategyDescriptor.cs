// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Registration metadata for one compaction strategy.</summary>
public sealed record CompactionStrategyDescriptor
{
    /// <summary>Initializes a validated strategy descriptor.</summary>
    /// <param name="key">The stable strategy key.</param>
    /// <param name="version">The strategy implementation version.</param>
    /// <param name="capabilities">Advertised strategy capabilities.</param>
    /// <param name="deterministic">Whether identical input yields identical output.</param>
    /// <param name="summaryGeneratorKey">The summary generator required by a model-backed strategy, if any.</param>
    /// <exception cref="ArgumentOutOfRangeException">A required identity is default.</exception>
    public CompactionStrategyDescriptor(
        CompactionStrategyKey key,
        CompactionStrategyVersion version,
        CompactionStrategyCapabilities capabilities,
        bool deterministic,
        CompactionSummaryGeneratorKey? summaryGeneratorKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        Key = key;
        Version = version;
        Capabilities = capabilities;
        Deterministic = deterministic;
        SummaryGeneratorKey = summaryGeneratorKey;
    }

    /// <summary>Gets the stable strategy key.</summary>
    public CompactionStrategyKey Key { get; }

    /// <summary>Gets the strategy implementation version.</summary>
    public CompactionStrategyVersion Version { get; }

    /// <summary>Gets the advertised strategy capabilities.</summary>
    public CompactionStrategyCapabilities Capabilities { get; }

    /// <summary>Gets a value indicating whether identical input yields identical output.</summary>
    public bool Deterministic { get; }

    /// <summary>Gets the summary generator required by a model-backed strategy, if any.</summary>
    public CompactionSummaryGeneratorKey? SummaryGeneratorKey { get; }
}
