// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Delegates root services while throwing whenever the facade requests a run scope.</summary>
internal sealed class ThrowingScopeServiceProvider(IServiceProvider inner): IServiceProvider, IServiceScopeFactory
{
    /// <inheritdoc/>
    public object? GetService(Type serviceType) => serviceType == typeof(IServiceScopeFactory)
        ? this
        : inner.GetService(serviceType);

    /// <inheritdoc/>
    public IServiceScope CreateScope() => throw new InvalidOperationException("Scope creation failed.");
}
