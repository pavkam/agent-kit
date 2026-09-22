// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Registers the root-jailed, sandbox-required operating-system process boundary.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one process resolver/runner and the supported platform sandbox profile.</summary>
        /// <param name="rootDirectory">The existing absolute workspace root.</param>
        /// <param name="configure">Additional executable allowlist and resource ceilings.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="rootDirectory"/> is blank.</exception>
        [Obsolete("Legacy host surface.")]
        public IServiceCollection AddOperatingSystemProcesses(
                    string rootDirectory,
                    Action<OperatingSystemProcessOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
            ArgumentNullException.ThrowIfNull(configure);
            _ = services.AddAgentKitObservability();
            _ = services.AddOptions<OperatingSystemProcessOptions>()
                .Configure(options => options.RootDirectory = rootDirectory)
                .Configure(configure)
                .Validate(static options => Path.IsPathRooted(options.RootDirectory), "RootDirectory must be absolute.")
                .Validate(static options => options.AllowedExecutablePaths.Count > 0, "At least one executable is required.")
                .Validate(
                    static options => options.ReadOnlyToolchainRoots.All(static item =>
                        !string.IsNullOrWhiteSpace(item.Key)
                        && !string.IsNullOrWhiteSpace(item.Value)
                        && Path.IsPathRooted(item.Value)),
                    "ReadOnlyToolchainRoots must have non-blank identities and absolute paths.")
                .Validate(static options => options.MaximumArgumentCount > 0, "MaximumArgumentCount must be positive.")
                .Validate(static options => options.MaximumArgumentBytes > 0, "MaximumArgumentBytes must be positive.")
                .Validate(static options => options.MaximumInputBytes >= 0, "MaximumInputBytes must be non-negative.")
                .Validate(
                    static options => options.MaximumEnvironmentBytes >= 0,
                    "MaximumEnvironmentBytes must be non-negative.")
                .Validate(static options => options.MaximumTimeout > TimeSpan.Zero, "MaximumTimeout must be positive.")
                .Validate(
                    static options => options.MaximumOutputBytes is > 0 and <= int.MaxValue,
                    "MaximumOutputBytes must be positive and fit in an Int32.")
                .Validate(
                    static options => options.MaximumArtifactOutputBytes is > 0 and <= int.MaxValue,
                    "MaximumArtifactOutputBytes must be positive and fit in an Int32.")
                .Validate(static options => options.MaximumConcurrentProcesses > 0, "Concurrency must be positive.")
                .Validate(
                    static options => options.ForcedTerminationWait > TimeSpan.Zero,
                    "ForcedTerminationWait must be positive.")
                .Validate(static options => options.MaximumExecutableBytes > 0, "MaximumExecutableBytes must be positive.")
                .ValidateOnStart();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<ProcessOperationId>, GuidProcessOperationIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
            services.TryAddSingleton<IProcessIntentResolver, OperatingSystemProcessIntentResolver>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessSandboxProvider, PlatformProcessSandboxProvider>());
            services.TryAddSingleton<IProcessRunner>(static provider => new OperatingSystemProcessRunner(
                provider.GetRequiredService<IProcessIntentResolver>(),
                provider.GetServices<IProcessSandboxProvider>(),
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IOptions<OperatingSystemProcessOptions>>(),
                provider.GetService<ILogger<OperatingSystemProcessRunner>>(),
                provider.GetService<IProcessOutputArtifactSink>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>()));
            return services;
        }
    }
}
