// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Exposes the immutable declared descriptors for every explicitly composed session store.</summary>
/// <remarks>Catalog discovery reports declarations for validation and diagnostics only. It never returns store implementations or grants authority to call them.</remarks>
public interface ISessionStoreCatalog
{
    /// <summary>Gets every explicitly registered store descriptor in deterministic key order.</summary>
    /// <returns>An immutable snapshot with no duplicate keys.</returns>
    public ImmutableArray<SessionStoreDescriptor> GetDescriptors();
}
