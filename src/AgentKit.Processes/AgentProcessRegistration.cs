// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Keyed dependency-injection registration for operating-system process executors.</summary>
internal static class AgentProcessRegistration
{
    internal static IServiceCollection Add(
        IServiceCollection services,
        ProcessExecutorKey key,
        Action<AgentProcessOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        _ = services.AddAgentKitObservability();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<ProcessOperationId>, GuidProcessOperationIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>, GuidSecurityAuditRecordIdGenerator>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessSandboxProvider, PlatformProcessSandboxProvider>());
        services.TryAddSingleton<IProcessSandboxSelector>(static provider =>
            new DefaultProcessSandboxSelector(provider.GetServices<IProcessSandboxProvider>()));

        var options = new AgentProcessOptions();
        configure(options);
        ValidateOptions(options);
        var snapshot = new AgentProcessOptionsSnapshot(
            key,
            new ProcessExecutorVersion(1),
            CloneOperatingSystemOptions(options.OperatingSystem),
            options.DefaultWorkspaceAccess,
            options.DefaultChildPolicy);
        var operatingSystemOptions = Options.Create(snapshot.OperatingSystem);
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

    private static void ValidateOptions(AgentProcessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OperatingSystem.RootDirectory);
        ArgumentOutOfRangeException.ThrowIfUndefined(options.DefaultWorkspaceAccess);
        ArgumentOutOfRangeException.ThrowIfUndefined(options.DefaultChildPolicy);
        if (options.OperatingSystem.AllowedExecutablePaths.Count == 0)
        {
            throw new InvalidOperationException("At least one allowed executable path is required.");
        }
    }

    private static OperatingSystemProcessOptions CloneOperatingSystemOptions(OperatingSystemProcessOptions source)
    {
        var clone = new OperatingSystemProcessOptions
        {
            RootDirectory = source.RootDirectory,
            MaximumArgumentCount = source.MaximumArgumentCount,
            MaximumArgumentBytes = source.MaximumArgumentBytes,
            MaximumInputBytes = source.MaximumInputBytes,
            MaximumEnvironmentBytes = source.MaximumEnvironmentBytes,
            MaximumTimeout = source.MaximumTimeout,
            MaximumOutputBytes = source.MaximumOutputBytes,
            MaximumArtifactOutputBytes = source.MaximumArtifactOutputBytes,
            MaximumConcurrentProcesses = source.MaximumConcurrentProcesses,
            ForcedTerminationWait = source.ForcedTerminationWait,
            MaximumExecutableBytes = source.MaximumExecutableBytes,
        };
        clone.AllowedExecutablePaths.AddRange(source.AllowedExecutablePaths);
        clone.AllowedEnvironmentVariableNames.AddRange(source.AllowedEnvironmentVariableNames);
        foreach (var (rootKey, rootPath) in source.ReadOnlyToolchainRoots)
        {
            clone.ReadOnlyToolchainRoots[rootKey] = rootPath;
        }

        return clone;
    }

    private static OperatingSystemExecutableResolver CreateExecutableResolver(IServiceProvider provider, object serviceKey)
    {
        var snapshot = provider.GetRequiredKeyedService<AgentProcessOptionsSnapshot>(serviceKey);
        var intentResolver = new OperatingSystemProcessIntentResolver(
            Options.Create(snapshot.OperatingSystem),
            provider.GetService<ILogger<OperatingSystemProcessIntentResolver>>());
        return new OperatingSystemExecutableResolver(snapshot, intentResolver);
    }

    private static OperatingSystemProcessExecutor CreateExecutor(IServiceProvider provider, object serviceKey)
    {
        var snapshot = provider.GetRequiredKeyedService<AgentProcessOptionsSnapshot>(serviceKey);
        return new OperatingSystemProcessExecutor(
            snapshot,
            provider.GetRequiredKeyedService<IExecutableResolver>(serviceKey),
            provider.GetRequiredService<IProcessSandboxSelector>(),
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<ISecurityAuditDispatcher>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>());
    }
}
