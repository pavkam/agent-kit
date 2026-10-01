// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Supplies one isolated goal-store composition and the owners whose goals it may hold.</summary>
/// <remarks>The fixture is constructed per case and owns every store, authority, and storage root it creates. A session-backed adapter creates real sessions in <see cref="CreateOwnerAsync"/>; other adapters only mint identities.</remarks>
public interface IGoalStoreConformanceFixture
{
    /// <summary>Gets the capabilities the adapter claims; durability cases run only when it claims durability.</summary>
    public ConformanceCapabilities Capabilities { get; }

    /// <summary>Gets a value indicating whether the adapter persists the captured delegation with a child goal.</summary>
    /// <value>False for a projection that stores only goal, attempt, and transition evidence.</value>
    public bool PreservesDelegation { get; }

    /// <summary>Gets the scanner identity the adapter was configured with, or null when it offers no intent discovery.</summary>
    public ComponentId? IntentScanner { get; }

    /// <summary>Gets the grant harness shared with the store under test.</summary>
    public TestGoalGrants Grants { get; }

    /// <summary>Gets the store under test.</summary>
    public IGoalStore Store { get; }

    /// <summary>Creates an owner in one tenant, including any session the adapter needs.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="cancellationToken">Cancels creation.</param>
    /// <returns>A fresh owner whose authorization scope is exactly itself.</returns>
    public ValueTask<GoalConformanceOwner> CreateOwnerAsync(string tenant, CancellationToken cancellationToken = default);

    /// <summary>Opens a fresh store instance over the same storage, as a restarted host would.</summary>
    /// <param name="cancellationToken">Cancels reopening.</param>
    /// <returns>A new store over the persisted state, or the same store when the adapter is ephemeral.</returns>
    public ValueTask<IGoalStore> ReopenAsync(CancellationToken cancellationToken = default);
}
