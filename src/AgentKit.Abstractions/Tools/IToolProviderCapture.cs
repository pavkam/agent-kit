// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns one retained source publication and acquisitions of its exact invoker bindings.</summary>
/// <remarks>
/// The snapshot is immutable evidence; this capture supplies the separate live lifetime.
/// Acquisition never invokes a tool or grants authority. Implementations are safe for concurrent
/// acquisition and closure. Closing prevents new acquisitions and waits for outstanding leases
/// before releasing resources owned by the capture; borrowed host resources keep their original owner.
/// Callers release outstanding leases before awaiting closure on the same control path.
/// Repeated disposal observes the same cleanup completion and failure. Disposal cannot be cancelled
/// because abandoning a waiter must not abandon resource ownership.
/// </remarks>
public interface IToolProviderCapture: IAsyncDisposable
{
    /// <summary>Gets the exact source publication retained throughout this capture's lifetime.</summary>
    /// <value>Stable immutable evidence, still readable after closure, without provider I/O or live metadata refresh.</value>
    public ToolProviderSnapshot Snapshot { get; }

    /// <summary>Acquires one owned lease to a binding from this exact source publication.</summary>
    /// <param name="identity">The nondefault canonical tool identity and exact version already established by resolution.</param>
    /// <param name="cancellationToken">Cancels acquisition before ownership transfers; it does not cancel an acquired lease.</param>
    /// <returns>An acquired lease, or an unavailable result retaining the requested identity when absent or closed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="identity"/> is default.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before acquisition commits.</exception>
    /// <remarks>
    /// Success transfers one lease to the caller. Failure and cancellation transfer no resource.
    /// A later publication cannot redirect an acquisition. Closing this capture does not invalidate
    /// previously acquired leases, prove that an external effect stopped, or authorize another invocation.
    /// </remarks>
    public ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(ToolIdentity identity, CancellationToken cancellationToken = default);

}
