// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures optional, untrusted scheduling and approval-cache hints for one tool.</summary>
/// <remarks>
/// Null optional values and <see cref="ToolSchedulingMode.Unspecified"/> mean no claim was made. Consumers may
/// tighten these hints but must never treat absent evidence as parallel safety, approval reuse, or a duration bound.
/// </remarks>
public sealed record ToolExecutionHints
{
    /// <summary>Initializes immutable tool execution hints.</summary>
    /// <param name="schedulingMode">The defined scheduling compatibility mode.</param>
    /// <param name="concurrencyKey">The nonblank key required exactly for <see cref="ToolSchedulingMode.ConcurrencyKey"/>.</param>
    /// <param name="expectedDuration">The optional nonnegative expected duration.</param>
    /// <param name="approvalMayBeCached">Whether approval caching is claimed, or null when unasserted.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="schedulingMode"/> is undefined or <paramref name="expectedDuration"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="concurrencyKey"/> is blank or inconsistent with <paramref name="schedulingMode"/>.</exception>
    public ToolExecutionHints(ToolSchedulingMode schedulingMode, string? concurrencyKey, TimeSpan? expectedDuration, bool? approvalMayBeCached)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(schedulingMode);
        if (concurrencyKey is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(concurrencyKey);
        }

        ArgumentException.ThrowIfNotEqual(schedulingMode == ToolSchedulingMode.ConcurrencyKey, concurrencyKey is not null, nameof(concurrencyKey));
        if (expectedDuration is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(expectedDuration));
        }

        SchedulingMode = schedulingMode;
        ConcurrencyKey = concurrencyKey;
        ExpectedDuration = expectedDuration;
        ApprovalMayBeCached = approvalMayBeCached;
    }

    /// <summary>Gets the declared scheduling compatibility mode.</summary>
    /// <value>A defined untrusted mode; unspecified supplies no evidence of safe overlap.</value>
    public ToolSchedulingMode SchedulingMode { get; }
    /// <summary>Gets the concurrency-key scheduling partition.</summary>
    /// <value>A nonblank ordinal key exactly for concurrency-key scheduling; otherwise null.</value>
    public string? ConcurrencyKey { get; }
    /// <summary>Gets the optional expected execution duration.</summary>
    /// <value>A nonnegative advisory duration, or null when unasserted; it is not an execution deadline.</value>
    public TimeSpan? ExpectedDuration { get; }
    /// <summary>Gets the approval-cacheability claim.</summary>
    /// <value>An advisory true or false claim, or null when unasserted; security policy remains authoritative.</value>
    public bool? ApprovalMayBeCached { get; }
}
