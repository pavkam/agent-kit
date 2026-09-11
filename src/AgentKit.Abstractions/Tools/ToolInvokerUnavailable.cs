// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that an exact invoker binding cannot be acquired without selecting an alternative.</summary>
/// <remarks>This result owns no resource. Its explanation is safe content selected under the caller's result bounds, not an exception dump.</remarks>
public sealed record ToolInvokerUnavailable: ToolInvokerLeaseResult
{
    /// <summary>Retains the exact requested identity and a safe explanation of unavailability.</summary>
    /// <param name="identity">The nondefault requested canonical identity and version.</param>
    /// <param name="safeReason">The nonblank, bounded safe explanation selected by the publisher.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="identity"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="safeReason"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is empty or whitespace.</exception>
    public ToolInvokerUnavailable(ToolIdentity identity, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(identity, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Identity = identity;
        SafeReason = safeReason;
    }

    /// <summary>Gets the exact identity that was unavailable.</summary>
    /// <value>The requested version; it is never replaced by a newer publication.</value>
    public ToolIdentity Identity { get; }

    /// <summary>Gets the safe explanation retained for the caller.</summary>
    /// <value>Nonblank content; consumers still enforce their own bounds before durable or model publication.</value>
    public string SafeReason { get; }
}
