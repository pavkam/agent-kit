// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports failed admission without creating an event subscription or run identity.</summary>
/// <typeparam name="TOutput">The requested output type.</typeparam>
public sealed record AgentRunStreamRejected<TOutput>: AgentRunStreamStartResult<TOutput>
{
    /// <summary>Captures the same rejection envelope used by nonstreaming admission.</summary>
    /// <param name="rejection">The nonnull pre-admission rejection.</param>
    /// <exception cref="ArgumentNullException">The rejection is null.</exception>
    public AgentRunStreamRejected(AgentRunRejected<TOutput> rejection) { ArgumentNullException.ThrowIfNull(rejection); Rejection = rejection; }
    /// <summary>Gets the normalized pre-admission rejection without a fabricated run.</summary>
    /// <value>The original nonnull rejection envelope.</value>
    public AgentRunRejected<TOutput> Rejection { get; }
}
