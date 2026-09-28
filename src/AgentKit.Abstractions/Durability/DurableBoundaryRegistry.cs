// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Collections.Concurrent;

/// <summary>Bridges a live in-process boundary to the durable operation handler the coordinator invokes for it.</summary>
/// <remarks>
/// <para>
/// The durability coordinator owns the order of acceptance, dispatch, and terminal commit, and invokes the handler
/// registered for an operation name to perform the effect. Many first-party boundaries — a model attempt, a tool
/// call, an input promotion, a compaction activation, an approval wait — are continuations over live state that no
/// payload can reconstruct, so the running component publishes that continuation here for the exact operation
/// identity it is about to declare and removes it as soon as the operation settles.
/// </para>
/// <para>
/// A recovering process finds no continuation, which is the honest answer: this process never prepared that
/// attempt. Recovery that only needs to commit an already-recorded terminal result never reaches the handler at
/// all, so the absence here blocks reinvocation without blocking the safe recovery paths.
/// </para>
/// <para>
/// The registry is an engine-wide singleton and is safe for concurrent use by every active run. It holds live
/// delegates only, never durable state, and is therefore not a persistence adapter.
/// </para>
/// </remarks>
public sealed class DurableBoundaryRegistry
{
    private readonly ConcurrentDictionary<OperationId, DurableBoundaryInvocation> _live = new();

    /// <summary>Publishes the live continuation for one operation identity until the returned lease is disposed.</summary>
    /// <param name="operationId">The exact nondefault operation identity the coordinator will invoke.</param>
    /// <param name="invoke">The non-null continuation that performs the effect under the acquired lease.</param>
    /// <returns>A lease that removes the continuation when disposed; disposal is idempotent and thread-safe.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="invoke"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationId"/> is the default, empty identity.</exception>
    /// <exception cref="InvalidOperationException">A continuation is already published for <paramref name="operationId"/>.</exception>
    /// <remarks>
    /// Publishing is exclusive per identity, because two continuations for one durable operation would make the
    /// journaled record ambiguous about which effect it describes.
    /// </remarks>
    public IDisposable Register(OperationId operationId, DurableBoundaryInvocation invoke)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default, nameof(operationId));
        ArgumentNullException.ThrowIfNull(invoke);
        return _live.TryAdd(operationId, invoke)
            ? new Lease(this, operationId)
            : throw new InvalidOperationException(
                "A live durable continuation is already registered for this operation identity.");
    }

    /// <summary>Resolves the live continuation published for one operation identity.</summary>
    /// <param name="operationId">The operation identity the coordinator is invoking.</param>
    /// <returns>The published continuation, or <see langword="null"/> when this process holds none.</returns>
    /// <remarks>A null result is the normal outcome in a recovering process and never means the operation is unknown.</remarks>
    public DurableBoundaryInvocation? Resolve(OperationId operationId) =>
        _live.TryGetValue(operationId, out var invocation) ? invocation : null;

    /// <summary>Removes one published continuation exactly once.</summary>
    private sealed class Lease(DurableBoundaryRegistry owner, OperationId operationId): IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _ = owner._live.TryRemove(operationId, out _);
            }
        }
    }
}
