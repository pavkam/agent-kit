// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes an interchangeable run coordinator over one real protected accepted-state path.</summary>
public interface ISessionRunCoordinatorConformanceFixture: IAsyncDisposable
{
    /// <summary>Creates, provisions, admits, accepts, and reloads one operation before returning its ownership scenario.</summary>
    /// <param name="cancellationToken">Cancels the setup before it completes.</param>
    /// <returns>A complete accepted operation suitable for reusable ownership assertions.</returns>
    public ValueTask<SessionRunCoordinatorConformanceScenario> CreateAcceptedScenarioAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Creates two accepted operations in different lanes of the same tenant/session partition.</summary>
    /// <param name="cancellationToken">Cancels setup before either scenario is returned.</param>
    /// <returns>Two scenarios that must acquire independently through one run coordinator.</returns>
    public ValueTask<SessionRunCoordinatorConformancePair> CreateDifferentLaneScenariosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Creates two accepted operations with colliding agent/session/lane coordinates in different tenants.</summary>
    /// <param name="cancellationToken">Cancels setup before either scenario is returned.</param>
    /// <returns>Two tenant-partitioned scenarios that must never block or disclose one another.</returns>
    public ValueTask<SessionRunCoordinatorConformancePair> CreateDifferentTenantScenariosAsync(
        CancellationToken cancellationToken = default);
}
