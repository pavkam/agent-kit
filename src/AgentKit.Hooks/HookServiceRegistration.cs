// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Package-internal dependency-injection registration for the hook kernel.</summary>
internal static class HookServiceRegistration
{
    internal static IServiceCollection AddAgentHooks(IServiceCollection services, Action<AgentHookOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentKitObservability();
        var options = services.AddOptions<AgentHookOptions>()
            .Validate(static value => value.MaximumInvocationDepth >= 1, "MaximumInvocationDepth must be at least 1.")
            .Validate(static value => Enum.IsDefined(value.MinimumFailureMode), "MinimumFailureMode must be a defined value.")
            .Validate(static value => value.DefaultHookTimeout > TimeSpan.Zero, "DefaultHookTimeout must be greater than zero.")
            .Validate(static value => Enum.IsDefined(value.MutationDispatchMode), "MutationDispatchMode must be a defined value.")
            .Validate(
                static value => value.MutationDispatchMode == HookMutationDispatchMode.Sequential,
                "Concurrent mutation dispatch is not supported yet.")
            .Validate(static value => Enum.IsDefined(value.ReloadBoundary), "ReloadBoundary must be a defined value.")
            .ValidateOnStart();
        if (configure is not null)
        {
            _ = options.Configure(configure);
        }

        _ = services.AddOptions<HookProfileOptions>();
        services.TryAddSingleton<IHookOrderResolver, HookOrderResolver>();
        services.TryAddSingleton<IHookProfileSelector, DefaultHookProfileSelector>();
        services.TryAddSingleton<IHookCatalog, HookRegistrationCatalog>();
        services.TryAddSingleton<HookRegistrationBindingRegistry>();
        services.TryAddSingleton<HookRegistrationBindingRegistryInitializer>();
        services.TryAddSingleton<IHookInstanceFactory>(static provider => new ServiceProviderHookInstanceFactory(
            provider,
            provider.GetRequiredService<HookRegistrationBindingRegistry>().Bindings,
            provider.GetRequiredService<IOptions<AgentHookOptions>>()));
        services.TryAddSingleton<IHookRegistrationSource, HookRegistrationBindingSource>();
        services.TryAddSingleton<IIdentifierGenerator<HookDispatchId>>(
            static _ => new GuidIdentifierGenerator<HookDispatchId>(static value => new HookDispatchId(value)));
        services.TryAddSingleton<IIdentifierGenerator<HookInvocationId>>(
            static _ => new GuidIdentifierGenerator<HookInvocationId>(static value => new HookInvocationId(value)));

        RegisterBuiltInPointDefinitions(services);
        services.TryAddSingleton<IHookDispatcher, DefaultHookDispatcher>();
        return services;
    }

    internal static IServiceCollection AddRunStartedHook<THook>(IServiceCollection services)
        where THook : class, IRunStartedHook
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = AddAgentHooks(services, configure: null);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRunStartedHook, THook>());
        AddBinding(services, typeof(THook), typeof(IRunStartedHook), AgentHookPoints.RunStarted);
        return services;
    }

    internal static IServiceCollection AddBeforeModelRequestHook<THook>(IServiceCollection services)
        where THook : class, IBeforeModelRequestHook
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = AddAgentHooks(services, configure: null);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBeforeModelRequestHook, THook>());
        AddBinding(services, typeof(THook), typeof(IBeforeModelRequestHook), AgentHookPoints.BeforeModelRequest);
        return services;
    }

    internal static IServiceCollection AddBeforeToolInvocationHook<THook>(IServiceCollection services)
        where THook : class, IBeforeToolInvocationHook
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = AddAgentHooks(services, configure: null);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBeforeToolInvocationHook, THook>());
        AddBinding(services, typeof(THook), typeof(IBeforeToolInvocationHook), AgentHookPoints.BeforeToolInvocation);
        return services;
    }

    private static void RegisterBuiltInPointDefinitions(IServiceCollection services)
    {
        services.TryAddSingleton(BuiltInAgentHookPointDefinitions.RunStarted);
        services.TryAddSingleton(BuiltInAgentHookPointDefinitions.BeforeModelRequest);
        services.TryAddSingleton(BuiltInAgentHookPointDefinitions.BeforeToolInvocation);
        services.TryAddSingleton<IReadOnlyList<HookPointDefinitionRegistration>>(static _ =>
        [
            BuiltInAgentHookPointDefinitions.RunStartedRegistration,
            BuiltInAgentHookPointDefinitions.BeforeModelRequestRegistration,
            BuiltInAgentHookPointDefinitions.BeforeToolInvocationRegistration,
        ]);
        services.TryAddSingleton<IHookMutationValidator<RunStartedEventArgs>, DefaultAgentHookMutationValidator<RunStartedEventArgs>>();
        services.TryAddSingleton<IHookMutationValidator<BeforeModelRequestEventArgs>, DefaultAgentHookMutationValidator<BeforeModelRequestEventArgs>>();
        services.TryAddSingleton<IHookMutationValidator<BeforeToolInvocationEventArgs>, DefaultAgentHookMutationValidator<BeforeToolInvocationEventArgs>>();
    }

    private static void AddBinding(
        IServiceCollection services,
        Type implementationType,
        Type hookServiceType,
        HookPointId point)
    {
        var binding = new HookRegistrationBinding(
            point,
            HookProfileOptions.DefaultProfileKey,
            implementationType,
            hookServiceType,
            HookLifetime.Singleton);
        services.TryAddEnumerable(ServiceDescriptor.Singleton(binding));
    }
}
