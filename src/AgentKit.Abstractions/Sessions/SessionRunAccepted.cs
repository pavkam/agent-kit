// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports newly committed or idempotently replayed complete accepted run state.</summary>
/// <remarks>
/// Acceptance installs the accepted state as the lane's single active operation. Every later
/// <see cref="ISessionStore.AcceptRunAsync"/> for the same lane returns <see cref="SessionRunStartBusy"/> naming the
/// installed operation and run until an explicit <see cref="ISessionStore.ReleaseRunAsync"/> clears it, while an
/// exact replay of the original request returns this result with <see cref="Existing"/> set to
/// <see langword="true"/>.
/// </remarks>
public sealed record SessionRunAccepted: SessionRunStartResult
{
    /// <summary>Initializes an acceptance receipt.</summary><param name="state">The complete committed accepted state.</param><param name="sessionVersion">The committed branch version.</param><param name="existing">Whether this is an equivalent replay.</param><exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    public SessionRunAccepted(SessionAcceptedRunState state, SessionVersion sessionVersion, bool existing) { ArgumentNullException.ThrowIfNull(state); State = state; SessionVersion = sessionVersion; Existing = existing; }
    /// <summary>Gets complete accepted state.</summary><value>The immutable recoverable state installed in the lane.</value>
    public SessionAcceptedRunState State { get; }
    /// <summary>Gets committed branch version.</summary><value>The version after atomic acceptance.</value>
    public SessionVersion SessionVersion { get; }
    /// <summary>Gets whether this is a replay.</summary><value>True when the identical prior acceptance was reconciled.</value>
    public bool Existing { get; }
}
