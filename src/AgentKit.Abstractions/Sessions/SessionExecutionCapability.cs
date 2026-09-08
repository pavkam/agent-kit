// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one compiled session profile to the exact selected coordinators for an invocation.</summary>
/// <remarks>This capability is invocation-only composition evidence. Callers retain ownership of it and must not cache it past the operation whose profile it captures.</remarks>
public sealed record SessionExecutionCapability
{
    /// <summary>Initializes a profile-bound coordinator capability.</summary>
    /// <param name="profile">The immutable compiled profile.</param>
    /// <param name="coordinator">The coordinator selected by <paramref name="profile"/>.</param>
    /// <param name="runCoordinator">The run coordinator selected by <paramref name="profile"/>.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public SessionExecutionCapability(SessionProfileSnapshot profile, ISessionCoordinator coordinator, ISessionRunCoordinator runCoordinator)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(coordinator); ArgumentNullException.ThrowIfNull(runCoordinator);
        Profile = profile; Coordinator = coordinator; RunCoordinator = runCoordinator;
    }

    /// <summary>Gets the immutable profile bound to this invocation.</summary><value>The exact compiled profile snapshot.</value>
    public SessionProfileSnapshot Profile { get; }
    /// <summary>Gets the selected session coordinator.</summary><value>The invocation's resolved coordinator; this capability does not own its lifetime.</value>
    public ISessionCoordinator Coordinator { get; }
    /// <summary>Gets the selected session-run coordinator.</summary><value>The invocation's resolved run coordinator; this capability does not own its lifetime.</value>
    public ISessionRunCoordinator RunCoordinator { get; }
}
