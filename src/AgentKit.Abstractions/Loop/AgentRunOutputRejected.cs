// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The run halted because terminal output was rejected or its selected configuration was invalid.</summary>
public sealed record AgentRunOutputRejected: AgentRunOutcome
{
    /// <summary>Initializes a candidate-rejection outcome.</summary>
    /// <param name="rejection">The exhausted candidate validation result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is null.</exception>
    public AgentRunOutputRejected(OutputRejected rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }

    /// <summary>Initializes a configuration-rejection outcome.</summary>
    /// <param name="rejection">The non-retriable output configuration result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is null.</exception>
    public AgentRunOutputRejected(OutputConfigurationRejected rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }

    /// <summary>Gets the typed non-success output-processing result.</summary>
    public OutputProcessingResult Rejection { get; }
}
