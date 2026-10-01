// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Contributes one named profile's options to the accumulating registry while the provider is built.</summary>
internal interface IMemoryProfileContributor
{
    /// <summary>Applies this contribution to the registry.</summary>
    /// <param name="registry">The registry being populated.</param>
    internal void Contribute(MemoryProfileRegistry registry);
}
