// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Contributes one profile configuration while the durability profile registry is initialized.</summary>
/// <remarks>
/// Contributors are additive singleton registrations applied in registration order exactly once, before any consumer
/// reads a profile. A contributor performs no service resolution and owns no disposable state.
/// </remarks>
internal interface IDurabilityProfileContributor
{
    /// <summary>Gets the profile key this contributor configures.</summary>
    public DurabilityProfileKey Key { get; }

    /// <summary>Applies this contributor's configuration to the shared registry.</summary>
    /// <param name="registry">The non-null registry being initialized.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    /// <exception cref="ArgumentException">The contributed configuration leaves a required profile value unset.</exception>
    public void Contribute(DurabilityProfileRegistry registry);
}
