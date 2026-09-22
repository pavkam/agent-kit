// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a resolved call's raw arguments could not be parsed or did not satisfy the declared schema.</summary>
/// <remarks>This outcome owns no resource. Its explanation is safe content selected by the validator, not an exception dump.</remarks>
public sealed record ToolCallValidationFailed: ToolArgumentValidationResult
{
    /// <summary>Retains the resolved call and a safe, bounded explanation of why validation failed.</summary>
    /// <param name="call">The nonnull resolved call whose arguments failed validation.</param>
    /// <param name="status">The defined pre-invocation terminal status describing the failure.</param>
    /// <param name="safeReason">The nonblank, bounded safe explanation selected by the validator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="call"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is null, empty, or whitespace.</exception>
    public ToolCallValidationFailed(ResolvedToolCall call, ToolTerminalStatus status, string safeReason)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Call = call;
        Status = status;
        SafeReason = safeReason;
    }

    /// <summary>Gets the resolved call whose arguments failed validation.</summary>
    /// <value>The exact resolved descriptor and raw arguments that were rejected.</value>
    public ResolvedToolCall Call { get; }

    /// <summary>Gets the pre-invocation terminal status describing the failure.</summary>
    /// <value>A defined status such as <see cref="ToolTerminalStatus.InvalidArguments"/>, <see cref="ToolTerminalStatus.ResourceLimitExceeded"/>, or <see cref="ToolTerminalStatus.Unsupported"/>.</value>
    public ToolTerminalStatus Status { get; }

    /// <summary>Gets the safe explanation retained for the caller.</summary>
    /// <value>Nonblank content; consumers still enforce their own bounds before durable or model publication.</value>
    public string SafeReason { get; }
}
