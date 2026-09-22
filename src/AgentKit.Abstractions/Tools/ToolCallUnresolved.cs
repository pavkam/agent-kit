// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a raw call's requested alias could not be resolved to an exact captured tool descriptor.</summary>
/// <remarks>This outcome owns no resource. Its explanation is safe content selected by the resolver, not an exception dump.</remarks>
public sealed record ToolCallUnresolved: ToolResolutionResult
{
    /// <summary>Retains the original request and a safe, bounded explanation of why resolution failed.</summary>
    /// <param name="request">The nonnull raw request that could not be resolved.</param>
    /// <param name="status">The defined pre-invocation terminal status describing the failure.</param>
    /// <param name="safeReason">The nonblank, bounded safe explanation selected by the resolver.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is null, empty, or whitespace.</exception>
    public ToolCallUnresolved(ToolCallRequest request, ToolTerminalStatus status, string safeReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Request = request;
        Status = status;
        SafeReason = safeReason;
    }

    /// <summary>Gets the raw request that could not be resolved.</summary>
    /// <value>The exact originating request; it is never replaced by a substitute identity.</value>
    public ToolCallRequest Request { get; }

    /// <summary>Gets the pre-invocation terminal status describing the failure.</summary>
    /// <value>A defined status such as <see cref="ToolTerminalStatus.UnknownTool"/> or <see cref="ToolTerminalStatus.Unsupported"/>.</value>
    public ToolTerminalStatus Status { get; }

    /// <summary>Gets the safe explanation retained for the caller.</summary>
    /// <value>Nonblank content; consumers still enforce their own bounds before durable or model publication.</value>
    public string SafeReason { get; }
}
