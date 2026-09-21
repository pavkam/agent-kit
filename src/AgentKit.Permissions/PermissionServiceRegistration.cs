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
        _ = services.AddAgentPermissions(configure: null);
        _ = EnsurePolicyCatalogRegistered(services);
        _ = services.RemoveAll<ISecurityPolicyCatalog>();
        return services.AddSingleton<ISecurityPolicyCatalog, TCatalog>();
    }

    internal static IServiceCollection ReplaceSecurityPolicySelector<TSelector>(IServiceCollection services)
        where TSelector : class, ISecurityPolicySelector
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentPermissions(configure: null);
        _ = EnsurePolicyCatalogRegistered(services);
        _ = services.RemoveAll<ISecurityPolicySelector>();
        return services.AddSingleton<ISecurityPolicySelector, TSelector>();
    }

    internal static IServiceCollection AddApprovalHandler<THandler>(IServiceCollection services)
        where THandler : class, IApprovalHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IApprovalHandler, THandler>();
    }

    internal static IServiceCollection ReplaceApprovalHandlerDispatcher<TDispatcher>(IServiceCollection services)
        where TDispatcher : class, IApprovalHandlerDispatcher
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentPermissions(configure: null);
        _ = services.RemoveAll<IApprovalHandlerDispatcher>();
        return services.AddSingleton<IApprovalHandlerDispatcher, TDispatcher>();
    }

    internal static IServiceCollection ReplaceApprovalBroker<TBroker>(IServiceCollection services)
        where TBroker : class, IApprovalBroker
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentPermissions(configure: null);
        _ = services.RemoveAll<IApprovalBroker>();
        return services.AddSingleton<IApprovalBroker, TBroker>();
    }

    internal static IServiceCollection ReplaceApprovalStore<TStore>(IServiceCollection services)
        where TStore : class, IApprovalStore
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.RemoveAll<IApprovalStore>();
        return services.AddSingleton<IApprovalStore, TStore>();
    }

    internal static IServiceCollection ReplaceSecurityGrantStore<TStore>(IServiceCollection services)
        where TStore : class, ISecurityGrantStore
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.RemoveAll<ISecurityGrantStore>();
        return services.AddSingleton<ISecurityGrantStore, TStore>();
    }

    internal static IServiceCollection ReplaceSecurityDecisionStore<TStore>(IServiceCollection services)
        where TStore : class, ISecurityDecisionStore
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.RemoveAll<ISecurityDecisionStore>();
        return services.AddSingleton<ISecurityDecisionStore, TStore>();
    }

    internal static IServiceCollection ReplaceSecurityGrantIssuer<TIssuer>(IServiceCollection services)
        where TIssuer : class, ISecurityGrantIssuer
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentPermissions(configure: null);
        _ = services.RemoveAll<ISecurityGrantIssuer>();
        return services.AddSingleton<ISecurityGrantIssuer, TIssuer>();
    }

    internal static IServiceCollection ReplaceSecurityAuditDispatcher<TDispatcher>(IServiceCollection services)
        where TDispatcher : class, ISecurityAuditDispatcher
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentPermissions(configure: null);
        _ = services.RemoveAll<ISecurityAuditDispatcher>();
        return services.AddSingleton<ISecurityAuditDispatcher, TDispatcher>();
    }
}
