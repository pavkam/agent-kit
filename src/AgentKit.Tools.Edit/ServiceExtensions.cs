// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit;

using AgentKit.Tools;

/// <summary>Registers the exact text-editing tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers editing and validates its complete-file bounds.</summary>
        /// <param name="configure">Optional byte-bound configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddEditTool(Action<EditToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<EditToolOptions>()
                .Validate(
                    static value => value.DefaultMaximumBytes > 0
                        && value.DefaultMaximumBytes <= value.MaximumBytes,
                    "Edit byte bounds are invalid.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<IIdentifierGenerator<WorkspaceMutationId>, GuidWorkspaceMutationIdGenerator>();
            _ = services.AddToolInvoker<EditTool>(EditTool.Descriptor);
            if (!services.Any(static descriptor =>
                    descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(ToolsetPublication)
                    && descriptor.ServiceKey is ToolsetKey key
                    && key == EditTool.DefaultToolset.Key))
            {
                _ = services.AddToolset(EditTool.DefaultToolset);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, EditTool>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolPresentationFormatter, EditToolPresentationFormatter>());
            return services;
        }
    }
}
