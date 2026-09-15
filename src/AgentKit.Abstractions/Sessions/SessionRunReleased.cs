// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a lane's accepted run state was cleared, or that an equivalent prior release was replayed.</summary>
/// <remarks>Releasing a lane does not append a session entry or change any branch tip; it clears only the lane's process-independent durable ownership marker so a later <see cref="ISessionStore.AcceptRunAsync"/> for the same lane no longer observes <see cref="SessionRunStartBusy"/>.</remarks>
public sealed record SessionRunReleased: SessionRunReleaseResult
{
    /// <summary>Initializes a release receipt.</summary>
    /// <param name="newVersion">The canonical whole-session version after release.</param>
    /// <param name="existing">Whether this is an equivalent replay of a previously committed release.</param>
    public SessionRunReleased(SessionVersion newVersion, bool existing)
    {
        NewVersion = newVersion;
        Existing = existing;
    }

    /// <summary>Gets the committed session version.</summary><value>The version after the atomic release.</value>
    public SessionVersion NewVersion { get; }

    /// <summary>Gets whether this is a replay.</summary><value><see langword="true"/> when an identical prior release was reconciled.</value>
    public bool Existing { get; }
}
