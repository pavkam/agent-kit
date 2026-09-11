// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports typed limit exhaustion with enforcement and partial-effect evidence.</summary>
/// <remarks>The immutable evidence is descriptive; its owner is responsible for safe normalization and actual lifecycle transitions.</remarks>
public sealed record RunLimitReached: AgentRunOutcome
{
    /// <summary>Captures a nonnull normalized reason without performing further work.</summary>
    /// <param name="limit">The nonnull immutable evidence to preserve.</param>
    /// <exception cref="ArgumentNullException">The evidence is null.</exception>
    public RunLimitReached(RunLimitFailure limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        Limit = limit;
    }
    /// <summary>Gets the original typed evidence without replacing its correlation or effect certainty.</summary>
    /// <value>The nonnull immutable reason captured at construction.</value>
    public RunLimitFailure Limit { get; }
}
