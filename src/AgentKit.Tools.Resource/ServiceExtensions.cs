// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource;

/// <summary>Registers configured resource loading as an independent tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers the resource tool and validates its configured bounds.</summary>
        /// <param name="configure">Optional resource definitions and host ceilings.</param>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddResourceTool(Action<ResourceToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<ResourceToolOptions>()
                .Validate(static value => value.MaximumBytes > 0, "MaximumBytes must be positive.")
                .Validate(static value => value.MaximumCharacters > 0, "MaximumCharacters must be positive.")
                .Validate(
                    static value => value.MaximumDescriptionCharacters > 0,
                    "MaximumDescriptionCharacters must be positive.")
                .Validate(
                    static value => value.Resources.Select(static resource => resource.Id).Distinct().Count()
                        == value.Resources.Count,
                    "Resource IDs must be unique.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ResourceTool>());
            return services;
        }
    }
}
