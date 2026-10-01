// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Carries one <c>AddMemoryProfile</c> or <c>ReplaceMemoryProfile</c> registration into the profile registry.</summary>
/// <param name="key">The profile key.</param>
/// <param name="configure">The configuration callback.</param>
/// <param name="replace">Whether the contribution discards earlier contributions for the key.</param>
internal sealed class MemoryProfileContributor(MemoryProfileKey key, Action<MemoryProfileOptions> configure, bool replace): IMemoryProfileContributor
{
    /// <inheritdoc/>
    public void Contribute(MemoryProfileRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Configure(key, configure, replace);
    }
}
