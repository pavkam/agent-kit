// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains one exact invoker binding until its owner finishes using and releases it.</summary>
/// <remarks>
/// A lease owns an acquisition, not necessarily the invoker instance. Disposing it once releases
/// that acquisition; shared or host-owned invokers are disposed only by their original owner.
/// The caller must finish all uses before release. Copying a lease reference does not acquire
/// another lifetime, and release cannot prove that a remote effect has stopped or been undone.
/// Concurrent and repeated disposal observe the same completion and failure, including owned cleanup
/// when this is the final acquisition, without disposing borrowed invokers directly.
/// </remarks>
public interface IToolInvokerLease: IAsyncDisposable
{
    /// <summary>Gets the complete immutable descriptor captured with this binding.</summary>
    /// <value>The exact tool identity, version, source, and metadata; readable after release without I/O.</value>
    public ToolDescriptor Tool { get; }

    /// <summary>Gets the exact source publication that supplied the binding.</summary>
    /// <value>A nondefault source version that cannot change when the provider refreshes.</value>
    public ToolSourceVersion SourceVersion { get; }

    /// <summary>Gets the borrowed invoker retained by this acquisition.</summary>
    /// <value>The stable instance available while the lease is open; it grants no invocation authority.</value>
    /// <exception cref="ObjectDisposedException">The lease has been released.</exception>
    public IToolInvoker Invoker { get; }

}
