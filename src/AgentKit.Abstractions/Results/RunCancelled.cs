// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports cancellation of accepted run work while preserving truthful partial evidence.</summary>
/// <remarks>The immutable evidence is descriptive; its owner is responsible for safe normalization and actual lifecycle transitions.</remarks>
public sealed record RunCancelled: AgentRunOutcome
{
    /// <summary>Captures a nonnull normalized reason without performing further work.</summary>
    /// <param name="reason">The nonnull immutable evidence to preserve.</param>
    /// <exception cref="ArgumentNullException">The evidence is null.</exception>
    public RunCancelled(CancellationReason reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        Reason = reason;
    }
    /// <summary>Gets the original typed evidence without replacing its correlation or effect certainty.</summary>
    /// <value>The nonnull immutable reason captured at construction.</value>
    public CancellationReason Reason { get; }
}
