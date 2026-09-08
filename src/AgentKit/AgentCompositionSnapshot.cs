// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Combines the exact run-profile and partial component-registration evidence validated for one engine composition.</summary>
/// <remarks>The engine retains this same immutable value so neither replaceable readiness readers nor later service-collection mutation can exchange evidence between validation and use.</remarks>
internal sealed record AgentCompositionSnapshot
{
    /// <summary>Initializes one exact validated composition snapshot.</summary>
    /// <param name="runProfiles">The non-null run-profile publication snapshot inspected by readiness validation.</param>
    /// <param name="componentRegistrations">The non-null component and Microsoft DI registration snapshot inspected by graph validation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runProfiles"/> or <paramref name="componentRegistrations"/> is <see langword="null"/>.</exception>
    internal AgentCompositionSnapshot(
        AgentRunProfilePublicationSnapshot runProfiles,
        ComponentRegistrationSnapshot componentRegistrations)
    {
        ArgumentNullException.ThrowIfNull(runProfiles);
        ArgumentNullException.ThrowIfNull(componentRegistrations);

        RunProfiles = runProfiles;
        ComponentRegistrations = componentRegistrations;
    }

    /// <summary>Gets the exact run-profile readiness evidence.</summary>
    /// <value>The same immutable snapshot inspected during validation.</value>
    internal AgentRunProfilePublicationSnapshot RunProfiles { get; }

    /// <summary>Gets the exact partial component-registration evidence.</summary>
    /// <value>The same build-local snapshot inspected by pure graph and DI correspondence validation.</value>
    internal ComponentRegistrationSnapshot ComponentRegistrations { get; }
}
