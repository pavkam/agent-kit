// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>
/// Validated process-wide defaults for the session coordinator and
/// run coordinator registered by <c>AddAgentSession</c>.
/// </summary>
/// <remarks>
/// These are hard engine ceilings validated once at registration time; they
/// are not re-read per operation. A future per-agent session profile may
/// tighten, but never loosen, these values.
/// </remarks>
public sealed class AgentSessionOptions
{
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
    /// Gets or sets what happens when a run requests the active-run lease
    /// for a session that already has one. Defaults to
    /// <see cref="SessionBusyBehavior.Reject"/>.
    /// </summary>
    public SessionBusyBehavior BusyBehavior { get; set; } = SessionBusyBehavior.Reject;

    /// <summary>
    /// Gets or sets how long <see cref="SessionBusyBehavior.Wait"/> waits
    /// for the active run to release the lease before giving up. Defaults
    /// to 30 seconds. Ignored when <see cref="BusyBehavior"/> is
    /// <see cref="SessionBusyBehavior.Reject"/>.
    /// </summary>
    public TimeSpan BusyWaitTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
