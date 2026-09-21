// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Package-internal dependency-injection registration for the security runtime.</summary>
internal static class PermissionServiceRegistration
{
    internal static IServiceCollection EnsurePolicyCatalogRegistered(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ISecurityPolicyCatalog>(static provider => new SecurityPolicyCatalog(
            provider.GetRequiredService<IOptions<AgentPermissionOptions>>(),
            provider.GetServices<SecurityProfilePublication>()));
        services.TryAddSingleton<ISecurityPolicySelector>(static provider => new DefaultSecurityPolicySelector(
            provider.GetRequiredService<ISecurityPolicyCatalog>(),
            provider.GetRequiredService<IOptions<AgentPermissionOptions>>()));
        return services;
    }

    internal static IServiceCollection ReplaceSecurityPolicyCatalog<TCatalog>(IServiceCollection services)
        where TCatalog : class, ISecurityPolicyCatalog
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = EnsurePolicyCatalogRegistered(services);
        _ = services.RemoveAll<ISecurityPolicyCatalog>();
        return services.AddSingleton<ISecurityPolicyCatalog, TCatalog>();
    }

    internal static IServiceCollection ReplaceSecurityPolicySelector<TSelector>(IServiceCollection services)
        where TSelector : class, ISecurityPolicySelector
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = EnsurePolicyCatalogRegistered(services);
        _ = services.RemoveAll<ISecurityPolicySelector>();
        return services.AddSingleton<ISecurityPolicySelector, TSelector>();
    }
}
