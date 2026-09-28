// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Empty service provider used by legacy compaction wiring.</summary>
internal sealed class EmptyServiceProvider: IServiceProvider
{
    /// <summary>Gets the shared empty instance.</summary>
    public static EmptyServiceProvider Instance { get; } = new();

    private EmptyServiceProvider()
    {
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType) => null;
}
