// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one compiled session profile to the exact coordinator instances selected for an invocation.</summary>
/// <remarks>The capability is immutable invocation evidence, not a service locator or security grant. Composition resolves both instances under the keys retained by <see cref="Profile"/> before constructing it; each protected effect still requires its own exact fresh authority.</remarks>
public sealed record SessionExecutionCapability
{
    /// <summary>Initializes one exact invocation-only session capability.</summary>
    /// <param name="profile">The immutable selected session profile and component keys.</param>
    /// <param name="coordinator">The coordinator resolved under <see cref="SessionProfileSnapshot.CoordinatorKey"/>.</param>
    /// <param name="runCoordinator">The run coordinator resolved under <see cref="SessionProfileSnapshot.RunCoordinatorKey"/>.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public SessionExecutionCapability(SessionProfileSnapshot profile, ISessionCoordinator coordinator,
        ISessionRunCoordinator runCoordinator)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(runCoordinator);
        Profile = profile;
        Coordinator = coordinator;
        RunCoordinator = runCoordinator;
    }

    /// <summary>Gets the exact compiled profile.</summary><value>Immutable profile identity, configuration, and component keys.</value>
    public SessionProfileSnapshot Profile { get; }
    /// <summary>Gets the selected protected session coordinator.</summary><value>The instance resolved under <see cref="SessionProfileSnapshot.CoordinatorKey"/>.</value>
    public ISessionCoordinator Coordinator { get; }
    /// <summary>Gets the selected lane-ownership coordinator.</summary><value>The instance resolved under <see cref="SessionProfileSnapshot.RunCoordinatorKey"/>.</value>
    public ISessionRunCoordinator RunCoordinator { get; }
}
