// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Lists the descriptors of every registered memory store.</summary>
/// <remarks>The catalog is discovery only. It selects nothing, grants nothing, and exposes no store instance.</remarks>
public interface IMemoryStoreCatalog
{
    /// <summary>Gets an immutable snapshot of registered memory stores.</summary>
    /// <returns>One descriptor per registered key, in deterministic key order.</returns>
    public ImmutableArray<MemoryStoreDescriptor> GetDescriptors();
}
