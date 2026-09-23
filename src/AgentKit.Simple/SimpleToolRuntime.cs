// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>Shared tool-runtime keys and helpers for Simple agents that select toolsets.</summary>
internal static class SimpleToolRuntime
{
    /// <summary>Gets the keyed <see cref="IToolExecutor"/> component identity Simple agents use with toolsets.</summary>
    internal static ComponentKey<IToolExecutor> ToolExecutorKey { get; } = new("agentkit.simple.tools");

    /// <summary>Gets the standard execution-policy family referenced by Simple toolset selections.</summary>
    internal static ToolExecutionPolicyKey StandardExecutionPolicyKey { get; } = new("standard");

    /// <summary>Registers discovery capture and the spec-shaped keyed executor when a plan selects toolsets.</summary>
    /// <param name="services">The builder service collection.</param>
    internal static void EnsureRegistered(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddToolDiscoveryRuntime();
        _ = services.ReplaceToolExecutor<DefaultToolExecutor>(ToolExecutorKey);
    }
}
