// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a store for one page of open delegated children, so a host worker can drain intents after process loss.</summary>
/// <remarks>
/// <para>
/// An open intent is a delegated child goal that is <see cref="GoalStatus.Ready"/> or <see cref="GoalStatus.Active"/>. The scan
/// crosses tenants, so it is not authorized by an operation grant: the store accepts it only from a scanner identity its
/// host configured. Results are ordered by creation sequence and each carries the persisted <see cref="DelegationRequest"/>,
/// from which the worker selects the captured authority for every subsequent operation.
/// </para>
/// </remarks>
public sealed record GoalIntentScanRequest
{
    /// <summary>Initializes a validated scan request.</summary>
    /// <param name="scanner">The host-configured scanner identity.</param>
    /// <param name="afterSequence">The exclusive creation-sequence cursor; zero starts at the first goal.</param>
    /// <param name="limit">The positive page size.</param>
    /// <exception cref="ArgumentException"><paramref name="scanner"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="afterSequence"/> is negative or <paramref name="limit"/> is not positive.</exception>
    public GoalIntentScanRequest(ComponentId scanner, long afterSequence, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scanner.Value, nameof(scanner));
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        Scanner = scanner;
        AfterSequence = afterSequence;
        Limit = limit;
    }

    /// <summary>Gets the host-configured scanner identity.</summary>
    public ComponentId Scanner { get; }

    /// <summary>Gets the exclusive creation-sequence cursor.</summary>
    public long AfterSequence { get; }

    /// <summary>Gets the page size.</summary>
    public int Limit { get; }
}
