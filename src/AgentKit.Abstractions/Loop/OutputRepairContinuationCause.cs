// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Indicates that output validation requested a bounded repair attempt.</summary>
public sealed record OutputRepairContinuationCause: RunContinuationCause
{
    /// <summary>Initializes output-repair evidence.</summary>
    /// <param name="decision">The typed processor decision requesting repair.</param>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null.</exception>
    public OutputRepairContinuationCause(OutputRetryRequired decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        Decision = decision;
    }

    /// <summary>Gets the bounded repair decision.</summary>
    public OutputRetryRequired Decision { get; }
}
