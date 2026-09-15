// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Composes isolated ephemeral approval storage through its public DI surface.</summary>
public sealed class InMemoryApprovalStoreConformanceFixture: IApprovalStoreConformanceFixture
{
    private ServiceProvider? _services;

    /// <inheritdoc/>
    public ValueTask<IApprovalStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var services = new ServiceCollection();
        _ = services.AddInMemoryApprovalStore();
        _services = services.BuildServiceProvider(validateScopes: true);
        return ValueTask.FromResult(_services.GetRequiredService<IApprovalStore>());
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _services?.Dispose();
        return ValueTask.CompletedTask;
    }
}
