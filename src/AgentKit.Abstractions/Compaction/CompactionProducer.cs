// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provenance for how a <see cref="CompactionCheckpoint"/> was produced.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </para>
/// <para>
/// The producer carries a strategy key and a determinism flag. The model,
/// provider, request, response, and usage provenance a model-backed strategy
/// must record belongs in <see cref="Extensions"/>; the first-party strategy
/// writes it under the stable keys of <c>ModelCompactionProvenanceKeys</c>.
/// </para>
/// </remarks>
public sealed record CompactionProducer
{
    /// <summary>Initializes a new instance of the <see cref="CompactionProducer"/> record.</summary>
    /// <param name="strategyKey">Which strategy produced the checkpoint.</param>
    /// <param name="deterministic">Whether the strategy is deterministic given identical input.</param>
    /// <param name="extensions">Caller-specific or forward-compatible provenance data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public CompactionProducer(CompactionStrategyKey strategyKey, bool deterministic, ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        StrategyKey = strategyKey;
        Deterministic = deterministic;
        Extensions = extensions;
    }

    /// <summary>Gets which strategy produced the checkpoint.</summary>
    public CompactionStrategyKey StrategyKey { get; init; }

    /// <summary>Gets a value indicating whether the strategy is deterministic given identical input.</summary>
    public bool Deterministic { get; init; }

    /// <summary>Gets caller-specific or forward-compatible provenance data.</summary>
    public ExtensionData Extensions { get; init; }
}
