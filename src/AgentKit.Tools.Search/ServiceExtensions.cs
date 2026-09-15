// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Search;

/// <summary>Registers the content-search tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers content search and validates all host ceilings.</summary>
        /// <param name="configure">Optional bound configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddSearchTool(Action<SearchToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<SearchToolOptions>()
                .Validate(static value => value.DefaultMaximumDepth > 0 && value.DefaultMaximumDepth <= value.MaximumDepth, "Depth bounds are invalid.")
                .Validate(static value => value.DefaultMaximumFiles > 0 && value.DefaultMaximumFiles <= value.MaximumFiles, "File bounds are invalid.")
                .Validate(static value => value.DefaultMaximumBytes > 0 && value.DefaultMaximumBytes <= value.MaximumBytes, "Byte bounds are invalid.")
                .Validate(static value => value.DefaultMaximumMatches > 0 && value.DefaultMaximumMatches <= value.MaximumMatches, "Match bounds are invalid.")
                .Validate(static value => value.DefaultMaximumLineBytes > 0 && value.DefaultMaximumLineBytes <= value.MaximumLineBytes, "Line bounds are invalid.")
                .Validate(static value => value.DefaultMaximumDuration > TimeSpan.Zero && value.DefaultMaximumDuration <= value.MaximumDuration, "Duration bounds are invalid.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, SearchTool>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolPresentationFormatter, SearchToolPresentationFormatter>());
            return services;
        }
    }
}
