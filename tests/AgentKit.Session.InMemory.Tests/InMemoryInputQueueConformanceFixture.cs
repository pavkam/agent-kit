// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using AgentKit.Conformance;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Composes the session-backed input queue over the real in-memory session store.</summary>
public sealed class InMemoryInputQueueConformanceFixture: SessionBackedInputQueueConformanceFixture
{
    /// <inheritdoc/>
    protected override string StoreKey => "agentkit.in-memory";

    /// <inheritdoc/>
    protected override void RegisterStore(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddInMemorySessionStore();
        _ = services.AddInMemorySessionDirectory(new ComponentId("conformance-directory"));
    }
}
