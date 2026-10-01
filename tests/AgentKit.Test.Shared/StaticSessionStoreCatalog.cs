// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="ISessionStoreCatalog"/> test double that publishes a fixed descriptor set.</summary>
/// <remarks>Initialized with the descriptors it returns, in order; an empty set is valid.</remarks>
/// <param name="descriptors">The descriptors to publish.</param>
public sealed class StaticSessionStoreCatalog(params SessionStoreDescriptor[] descriptors): ISessionStoreCatalog
{
    /// <inheritdoc/>
    public ImmutableArray<SessionStoreDescriptor> GetDescriptors() => [.. descriptors];
}
