// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Keyed dependency-injection registration for scripted process executors.</summary>
internal static class AgentScriptedProcessRegistration
{
    internal static IServiceCollection Add(
        IServiceCollection services,
        ProcessExecutorKey key,
        Action<ScriptedProcessOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        _ = services.AddAgentKitObservability();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();

        var options = new ScriptedProcessOptions();
        configure(options);
        ValidateOptions(options);
        ImmutableDictionary<ProcessOperationId, ScriptedProcessScenario> scenarios;
        try
        {
            scenarios = options.Scenarios.ToImmutableDictionary(static item => item.OperationId);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Scripted process operation identities must be unique.", nameof(configure), exception);
        }

        var snapshot = new ScriptedProcessOptionsSnapshot(
            key,
            new ProcessExecutorVersion(1),
            options,
            scenarios);
        services.TryAddKeyedSingleton(key.Value, snapshot);
        services.TryAddKeyedSingleton<IExecutableResolver>(key.Value, static (provider, serviceKey) =>
            CreateExecutableResolver(provider, serviceKey!));
        services.TryAddKeyedSingleton<IProcessExecutor>(key.Value, static (provider, serviceKey) =>
            CreateExecutor(provider, serviceKey!));
        _ = services.AddSingleton(new ProcessExecutorRegistration(key));
        services.TryAddSingleton<IProcessExecutorSelector>(static provider =>
            new DefaultProcessExecutorSelector(provider, provider.GetServices<ProcessExecutorRegistration>()));
        return services;
    }

    private static void ValidateOptions(ScriptedProcessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.WorkspaceRoot);
        if (!Path.IsPathRooted(options.WorkspaceRoot))
        {
            throw new InvalidOperationException("WorkspaceRoot must be absolute.");
        }

        if (options.Executables.Count == 0)
        {
            throw new InvalidOperationException("At least one executable mapping is required.");
        }
    }

    private static ScriptedProcessOptions CloneOptions(ScriptedProcessOptionsSnapshot snapshot)
    {
        var clone = new ScriptedProcessOptions { WorkspaceRoot = snapshot.WorkspaceRoot };
        clone.Executables.AddRange(snapshot.Executables.Values);
        clone.AllowedEnvironmentVariableNames.AddRange(snapshot.AllowedEnvironmentVariableNames);
        clone.Scenarios.AddRange(snapshot.Scenarios.Values);
        return clone;
    }

    private static ScriptedExecutableResolver CreateExecutableResolver(IServiceProvider provider, object serviceKey)
    {
        var snapshot = provider.GetRequiredKeyedService<ScriptedProcessOptionsSnapshot>(serviceKey);
        var intentResolver = new ScriptedProcessIntentResolver(
            Options.Create(CloneOptions(snapshot)),
            provider.GetService<ILogger<ScriptedProcessIntentResolver>>());
        return new ScriptedExecutableResolver(snapshot, intentResolver);
    }

    private static ScriptedProcessExecutor CreateExecutor(IServiceProvider provider, object serviceKey)
    {
        var snapshot = provider.GetRequiredKeyedService<ScriptedProcessOptionsSnapshot>(serviceKey);
        return new ScriptedProcessExecutor(
            snapshot,
            provider.GetRequiredKeyedService<IExecutableResolver>(serviceKey),
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
            provider.GetService<ILogger<ScriptedProcessExecutor>>());
    }
}
