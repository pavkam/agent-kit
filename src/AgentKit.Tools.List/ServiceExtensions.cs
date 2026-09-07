// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List;

/// <summary>Registers the directory-listing tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers the listing tool and validates its page configuration.</summary>
        /// <param name="configure">Optional page-bound configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
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

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ListDirectoryTool>());
            return services;
        }
    }
}
