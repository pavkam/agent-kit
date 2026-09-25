// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using AgentKit.Context;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers MCP catalog contributors for one endpoint binding.</summary>
public static class McpContextContributorRegistration
{
    /// <summary>Adds MCP resource and prompt context contributors for one endpoint binding.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="binding">The endpoint and profile binding.</param>
    /// <param name="assemblerKey">The assembler profile that receives the contributors.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddMcpContextContributors(
        IServiceCollection services,
        McpEndpointContextBinding binding,
        ComponentKey<IContextAssembler>? assemblerKey = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(binding);
        assemblerKey ??= AgentContextComponentDefaults.AssemblerKey;
        services.TryAddEnumerable(ServiceDescriptor.Singleton(binding));
        _ = services.AddContextContributor<McpResourceContextContributor>(
            (ComponentKey<IContextAssembler>) assemblerKey,
            new ContextContributorRegistration(
                new ContextSourceKey($"agentkit.mcp.resource.{binding.EndpointKey.Value}"),
                order: 70,
                ContextEvaluationFrequency.OncePerRun,
                required: false));
        _ = services.AddContextContributor<McpPromptContextContributor>(
            (ComponentKey<IContextAssembler>) assemblerKey,
            new ContextContributorRegistration(
                new ContextSourceKey($"agentkit.mcp.prompt.{binding.EndpointKey.Value}"),
                order: 71,
                ContextEvaluationFrequency.OncePerRun,
                required: false));
        return services;
    }
}

/// <summary>Captures one MCP endpoint used by context contributors at run time.</summary>
/// <param name="EndpointKey">The MCP endpoint key.</param>
/// <param name="CapabilityProfileId">The MCP capability profile id.</param>
public sealed record McpEndpointContextBinding(McpEndpointKey EndpointKey, CapabilityProfileId CapabilityProfileId);
