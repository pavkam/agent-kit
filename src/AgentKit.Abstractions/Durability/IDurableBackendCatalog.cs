// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Exposes every durable backend descriptor registered in the composition.</summary>
public interface IDurableBackendCatalog
{
    /// <summary>Gets the immutable descriptor set captured at composition time.</summary>
    /// <returns>All registered backend descriptors.</returns>
    public ImmutableArray<DurableBackendDescriptor> GetDescriptors();
}
