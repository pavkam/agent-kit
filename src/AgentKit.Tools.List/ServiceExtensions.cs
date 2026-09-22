// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List;

using AgentKit.Tools;

/// <summary>Registers the directory-listing tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers the listing tool and validates its page configuration.</summary>
        /// <param name="configure">Optional page-bound configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// Registers the spec-shaped <see cref="IToolInvoker"/> through <see cref="Tools.ServiceExtensions.AddToolInvoker{TInvoker}"/>,
        /// publishes <see cref="ListDirectoryTool.DefaultToolset"/>, and retains a legacy <see cref="ITool"/> registration until workstream 4
        /// chunk C10 removes the reduced catalog path.
        /// </remarks>
        public IServiceCollection AddListTool(Action<ListDirectoryToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<ListDirectoryToolOptions>()
                .Validate(static value => value.DefaultPageEntries > 0, "DefaultPageEntries must be positive.")
                .Validate(static value => value.MaximumPageEntries > 0, "MaximumPageEntries must be positive.")
                .Validate(
                    static value => value.DefaultPageEntries <= value.MaximumPageEntries,
                    "DefaultPageEntries must not exceed MaximumPageEntries.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            _ = services.AddToolInvoker<ListDirectoryTool>(ListDirectoryTool.Descriptor);
            if (!services.Any(static descriptor =>
                    descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(ToolsetPublication)
                    && descriptor.ServiceKey is ToolsetKey key
                    && key == ListDirectoryTool.DefaultToolset.Key))
            {
                _ = services.AddToolset(ListDirectoryTool.DefaultToolset);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ListDirectoryTool>());
            return services;
        }
    }
}
