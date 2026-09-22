// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that invocation evidence could not be normalized under the captured snapshot.</summary>
public sealed record ToolResultNormalizationFailed: ToolResultNormalizationResult
{
    /// <summary>Retains the terminal status and safe explanation for the normalization failure.</summary>
    /// <param name="status">The defined terminal status describing the failure.</param>
    /// <param name="safeReason">The nonblank safe explanation selected by the normalizer.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is null, empty, or whitespace.</exception>
    public ToolResultNormalizationFailed(ToolTerminalStatus status, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Status = status;
        SafeReason = safeReason;
    }

    /// <summary>Gets the terminal status describing the normalization failure.</summary>
    /// <value>A defined status such as <see cref="ToolTerminalStatus.ResultNormalizationFailed"/>.</value>
    public ToolTerminalStatus Status { get; }

    /// <summary>Gets the safe explanation retained for the caller.</summary>
    /// <value>Nonblank content suitable for model-visible error evidence.</value>
    public string SafeReason { get; }
}
