// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Applies one captured profile configuration callback to the durability profile registry.</summary>
/// <remarks>
/// The captured callback runs once during registry initialization on whichever thread builds the provider. A
/// contributor created with <paramref name="replace"/> discards configuration accumulated by earlier contributors for
/// the same key instead of refining it.
/// </remarks>
/// <param name="key">The profile key this contributor configures.</param>
/// <param name="configure">The configuration callback applied to the profile options.</param>
/// <param name="replace">Whether the contribution replaces rather than refines existing configuration.</param>
internal sealed class DurabilityProfileContributor(
    DurabilityProfileKey key,
    Action<DurabilityProfileOptions> configure,
    bool replace): IDurabilityProfileContributor
{
    /// <inheritdoc/>
    public DurabilityProfileKey Key { get; } = key;

    /// <inheritdoc/>
    public void Contribute(DurabilityProfileRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Configure(Key, configure, replace);
    }
}
