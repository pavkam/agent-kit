// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Reports the current best-known <see cref="Usage"/> for an in-progress
/// attempt. A later event's usage supersedes an earlier one; it never means
/// "additional usage on top of" a prior report.
/// </summary>
public sealed record ModelUsageUpdated: ModelResponseEvent
{
    /// <summary>Initializes a new instance of the <see cref="ModelUsageUpdated"/> record.</summary>
    /// <param name="requestId">The request this event belongs to.</param>
    /// <param name="sequence">The strictly increasing sequence number of this event within its request.</param>
    /// <param name="usage">The current best-known usage for the attempt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="usage"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="usage"/> represents absence rather than an observable update.</exception>
    public ModelUsageUpdated(ModelRequestId requestId, long sequence, ModelUsage usage)
        : base(requestId, sequence)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentException.ThrowIfNotEqual(
            usage.ReportState == ModelUsageReportState.NotReported,
            false,
            nameof(usage));
        Usage = usage;
    }

    /// <summary>Gets the current best-known usage for the attempt.</summary>
    public ModelUsage Usage { get; }
}
