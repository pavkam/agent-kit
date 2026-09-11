// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Transfers ownership of one accepted run's local event subscription to its caller.</summary>
/// <typeparam name="TOutput">The validated output snapshot type.</typeparam>
public sealed record AgentRunStreamStarted<TOutput>: AgentRunStreamStartResult<TOutput>
{
    /// <summary>Captures the nonnull stream returned after admission.</summary>
    /// <param name="stream">The accepted run's subscription, which the caller must dispose.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    public AgentRunStreamStarted(IAgentRunStream<TOutput> stream) { ArgumentNullException.ThrowIfNull(stream); Stream = stream; }
    /// <summary>Gets the caller-owned subscription to accepted work.</summary>
    /// <value>A nonnull stream whose disposal does not implicitly abort the run.</value>
    public IAgentRunStream<TOutput> Stream { get; }
}
