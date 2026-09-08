// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>
/// Validated process-wide defaults for the session coordinator and
/// run coordinator registered by <c>AddAgentSession</c>.
/// </summary>
/// <remarks>
/// These are hard engine ceilings captured by the registered coordinators and
/// are not re-read per operation. The immutable selected session profile owns
/// per-operation busy behavior and may tighten applicable append/page bounds.
/// </remarks>
public sealed class AgentSessionOptions
{
    /// <summary>Gets the largest wait representable by the injected timer scheduler.</summary>
    /// <value>The platform timer ceiling used by both direct construction and options validation.</value>
    internal static TimeSpan MaximumBusyWaitTimeout { get; } = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    /// <summary>
    /// Gets or sets the maximum number of entries one
    /// <see cref="SessionAppendRequest"/> may carry. Defaults to 128.
    /// </summary>
    public int MaximumAppendEntries { get; set; } = 128;

    /// <summary>
    /// Gets or sets the maximum page size one <see cref="SessionReadRequest"/>
    /// may request. Defaults to 256.
    /// </summary>
    public int MaximumPageSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the lifetime of each security request issued by the
    /// coordinator. Defaults to one minute and cannot exceed one hour.
    /// </summary>
    public TimeSpan SecurityRequestLifetime { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the legacy process default for busy behavior. Capability-based
    /// acquisition uses the immutable selected <see cref="SessionProfileSnapshot.BusyBehavior"/>
    /// instead. The default is <see cref="SessionBusyBehavior.Reject"/>.
    /// </summary>
    public SessionBusyBehavior BusyBehavior { get; set; } = SessionBusyBehavior.Reject;

    /// <summary>
    /// Gets or sets how long <see cref="SessionBusyBehavior.Wait"/> waits
    /// for the lane owner to release the lease before giving up. Defaults to 30
    /// seconds, must not be negative, and cannot exceed the platform timer
    /// ceiling. It is ignored when the selected session profile chooses
    /// <see cref="SessionBusyBehavior.Reject"/>.
    /// </summary>
    public TimeSpan BusyWaitTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
