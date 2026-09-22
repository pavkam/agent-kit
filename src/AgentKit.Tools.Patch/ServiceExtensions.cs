// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

using AgentKit.Tools;

/// <summary>Registers the parsed workspace-patch tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers patch execution and validates all parser and file bounds.</summary>
        /// <param name="configure">Optional patch-bound configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddPatchTool(Action<PatchToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<PatchToolOptions>()
                .Validate(
                    static value => value.MaximumPatchBytes > 0
                        && value.MaximumEntries > 0
                        && value.MaximumFileBytes > 0,
                    "Patch bounds must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<IIdentifierGenerator<WorkspaceMutationId>, GuidWorkspaceMutationIdGenerator>();
            _ = services.AddToolInvoker<PatchTool>(PatchTool.Descriptor);
            if (!services.Any(static descriptor =>
                    descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(ToolsetPublication)
                    && descriptor.ServiceKey is ToolsetKey key
                    && key == PatchTool.DefaultToolset.Key))
            {
                _ = services.AddToolset(PatchTool.DefaultToolset);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, PatchTool>());
            return services;
        }
    }
}
