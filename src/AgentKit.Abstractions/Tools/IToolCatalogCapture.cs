// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns the retained source acquisitions behind one immutable run-bound catalog.</summary>
/// <remarks>
/// Acquisitions use only this catalog's selected descriptors and exact source versions. They never
/// invoke a tool, resolve a provider alias, or grant authority. Concurrent closure prevents new
/// transfers and drains pending acquisitions and outstanding leases before releasing all owned sources.
/// Callers release their leases before awaiting closure on the same control path. Repeated disposal
/// shares completion and cleanup failure without retry. Disposal cannot be cancelled; abandoning a
/// waiter must not abandon ownership. Immutable metadata remains readable after closure.
/// </remarks>
public interface IToolCatalogCapture: IAsyncDisposable
{
    /// <summary>Gets the catalog evidence bound to this retained graph.</summary>
    /// <value>The stable immutable snapshot, without provider I/O or live metadata refresh.</value>
    public ToolCatalogSnapshot Snapshot { get; }

    /// <summary>Acquires an owned invoker lease for an exact descriptor selected in this catalog.</summary>
    /// <param name="identity">The nondefault canonical tool identity and version already established by resolution.</param>
    /// <param name="cancellationToken">Cancels acquisition before transfer; cancellation does not revoke an acquired lease.</param>
    /// <returns>An acquired lease matching the full captured descriptor and source version, or an unavailable result for the requested identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="identity"/> is default.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before transfer.</exception>
    /// <exception cref="InvalidOperationException">A retained source violates its acquisition contract.</exception>
    /// <remarks>
    /// Unknown, unselected, and closed acquisitions return unavailable without contacting a source.
    /// Failure, cancellation, and closure during acquisition release any obtained lease before returning.
    /// Cleanup failures remain observable and do not authorize a retry or prove an external effect stopped.
    /// </remarks>
    public ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(ToolIdentity identity, CancellationToken cancellationToken = default);
}
