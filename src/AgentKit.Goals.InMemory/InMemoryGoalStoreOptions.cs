// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.InMemory;

/// <summary>Configures an in-memory goal store.</summary>
public sealed class InMemoryGoalStoreOptions
{
    /// <summary>Gets the scanner identities allowed to call <see cref="IGoalStore.ReadIntentsAsync"/>.</summary>
    /// <value>A mutable list, empty by default: intent discovery crosses tenants and is refused unless the host names its worker here.</value>
    public List<ComponentId> AuthorizedIntentScanners { get; } = [];
}
