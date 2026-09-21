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

        services.TryAddSingleton(static _ => new HookProfileRegistry());
        services.TryAddSingleton<HookProfileRegistryInitializer>();
        services.TryAddSingleton<IHookOrderResolver, HookOrderResolver>();
        services.TryAddSingleton<IHookProfileSelector>(static provider =>
        {
            _ = provider.GetRequiredService<HookProfileRegistryInitializer>();
            return new DefaultHookProfileSelector(provider.GetRequiredService<HookProfileRegistry>());
        });
        services.TryAddSingleton<IHookDiagnosticDispatcher, HookDiagnosticDispatcher>();
        services.TryAddSingleton<IHookCatalog, HookRegistrationCatalog>();
        services.TryAddSingleton<HookRegistrationBindingRegistry>();
        services.TryAddSingleton<HookRegistrationBindingRegistryInitializer>();
        services.TryAddSingleton<IHookInstanceFactory>(static provider =>
        {
            _ = provider.GetRequiredService<HookRegistrationBindingRegistryInitializer>();
            _ = provider.GetRequiredService<HookProfileRegistry>();
            return new ServiceProviderHookInstanceFactory(
                provider,
                provider.GetRequiredService<HookRegistrationBindingRegistry>().Bindings,
                provider.GetRequiredService<IOptions<AgentHookOptions>>());
        });
        services.TryAddSingleton<IHookRegistrationSource>(static provider =>
        {
            _ = provider.GetRequiredService<HookRegistrationBindingRegistryInitializer>();
            return new HookRegistrationBindingSource(
                provider.GetRequiredService<HookRegistrationBindingRegistry>(),
                provider.GetRequiredService<HookProfileRegistry>(),
                provider.GetRequiredService<IReadOnlyList<HookPointDefinitionRegistration>>());
        });
        services.TryAddSingleton<IIdentifierGenerator<HookDispatchId>>(
            static _ => new GuidIdentifierGenerator<HookDispatchId>(static value => new HookDispatchId(value)));
        services.TryAddSingleton<IIdentifierGenerator<HookInvocationId>>(
            static _ => new GuidIdentifierGenerator<HookInvocationId>(static value => new HookInvocationId(value)));

        RegisterBuiltInPointDefinitions(services);
        services.TryAddSingleton<IHookDispatcher>(static provider => new DefaultHookDispatcher(
            provider.GetRequiredService<IOptions<AgentHookOptions>>(),
            provider.GetService<ILogger<DefaultHookDispatcher>>(),
            provider.GetRequiredService<IHookOrderResolver>(),
            provider.GetRequiredService<IIdentifierGenerator<HookInvocationId>>(),
            provider.GetRequiredService<IHookDiagnosticDispatcher>(),
            provider.GetService<TimeProvider>() ?? TimeProvider.System,
            provider.GetRequiredService<HookProfileRegistry>()));
        return services;
    }

    internal static IServiceCollection AddHookProfile(
        IServiceCollection services,
        HookProfileKey key,
        Action<HookProfileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(configure);
        _ = AddAgentHooks(services, configure: null);
        AddProfileContributor(services, key, configure, replace: false);
        return services;
    }

    internal static IServiceCollection ReplaceHookProfile(
        IServiceCollection services,
        HookProfileKey key,
        Action<HookProfileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(configure);
        _ = AddAgentHooks(services, configure: null);
        AddProfileContributor(services, key, configure, replace: true);
        return services;
    }

    internal static IServiceCollection AddHookDiagnosticSink<TSink>(IServiceCollection services)
        where TSink : class, IHookDiagnosticSink
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = AddAgentHooks(services, configure: null);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHookDiagnosticSink, TSink>());
        return services;
    }

    internal static IServiceCollection ReplaceHookDiagnosticDispatcher<TDispatcher>(IServiceCollection services)
        where TDispatcher : class, IHookDiagnosticDispatcher
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = AddAgentHooks(services, configure: null);
        _ = services.RemoveAll<IHookDiagnosticDispatcher>();
        return services.AddSingleton<IHookDiagnosticDispatcher, TDispatcher>();
    }

    private static void AddProfileContributor(
        IServiceCollection services,
        HookProfileKey key,
        Action<HookProfileOptions> configure,
        bool replace) =>
        _ = services.AddSingleton<IHookProfileContributor>(_ => new HookProfileContributor(key, configure, replace));

    internal static IServiceCollection AddRunStartedHook<THook>(IServiceCollection services, HookRegistrationDescriptor descriptor)
        where THook : class, IRunStartedHook
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNotEqual(descriptor.Point, AgentHookPoints.RunStarted);
        _ = AddAgentHooks(services, configure: null);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRunStartedHook, THook>());
        AddBinding<THook>(services, typeof(IRunStartedHook), descriptor);
        return services;
    }

    internal static IServiceCollection AddBeforeModelRequestHook<THook>(IServiceCollection services, HookRegistrationDescriptor descriptor)
        where THook : class, IBeforeModelRequestHook
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNotEqual(descriptor.Point, AgentHookPoints.BeforeModelRequest);
        _ = AddAgentHooks(services, configure: null);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBeforeModelRequestHook, THook>());
        AddBinding<THook>(services, typeof(IBeforeModelRequestHook), descriptor);
        return services;
    }

    internal static IServiceCollection AddBeforeToolInvocationHook<THook>(IServiceCollection services, HookRegistrationDescriptor descriptor)
        where THook : class, IBeforeToolInvocationHook
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNotEqual(descriptor.Point, AgentHookPoints.BeforeToolInvocation);
        _ = AddAgentHooks(services, configure: null);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBeforeToolInvocationHook, THook>());
        AddBinding<THook>(services, typeof(IBeforeToolInvocationHook), descriptor);
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

    private static void AddBinding<THook>(
        IServiceCollection services,
        Type hookServiceType,
        HookRegistrationDescriptor descriptor)
        where THook : class
    {
        var binding = new HookRegistrationBinding(descriptor, typeof(THook), hookServiceType);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHookRegistrationBindingContributor, HookRegistrationBindingContributor<THook>>(
            _ => new HookRegistrationBindingContributor<THook>(binding)));
    }
}
