// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The point, dispatch, causal, and timing facts shared by every hook invoked within one dispatch.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. <see cref="AgentHookEventArgs"/> carries these same facts to hooks; this record is the
/// dispatcher-facing counterpart the kernel builds once per dispatch and passes alongside the catalog and
/// activation lease in a <see cref="HookDispatchContext"/>.
/// </remarks>
public sealed record HookDispatchMetadata
{
    /// <summary>Initializes a new instance of the <see cref="HookDispatchMetadata"/> record.</summary>
    /// <param name="point">The hook point being dispatched.</param>
    /// <param name="dispatchId">This emission's stable identity.</param>
    /// <param name="correlation">The causal operation this dispatch occurred within.</param>
    /// <param name="timestamp">The time this dispatch began, from the injected <see cref="TimeProvider"/>.</param>
    /// <param name="deadline">The latest time by which every hook in this dispatch must have quiesced.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="point"/> or <paramref name="dispatchId"/> is default, or <paramref name="deadline"/> is not after <paramref name="timestamp"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="correlation"/> is null.</exception>
    public HookDispatchMetadata(
        HookPointId point,
        HookDispatchId dispatchId,
        OperationCorrelation correlation,
        DateTimeOffset timestamp,
        DateTimeOffset deadline)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(point, default);
        ArgumentOutOfRangeException.ThrowIfEqual(dispatchId, default);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deadline, timestamp);

        Point = point;
        DispatchId = dispatchId;
        Correlation = correlation;
        Timestamp = timestamp;
        Deadline = deadline;
    }

    /// <summary>Gets the hook point being dispatched.</summary>
    public HookPointId Point { get; }

    /// <summary>Gets this emission's stable identity.</summary>
    public HookDispatchId DispatchId { get; }

    /// <summary>Gets the causal operation this dispatch occurred within.</summary>
    public OperationCorrelation Correlation { get; }

    /// <summary>Gets the time this dispatch began.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets the latest time by which every hook in this dispatch must have quiesced.</summary>
    public DateTimeOffset Deadline { get; }
}
