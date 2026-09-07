// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Glob;

/// <summary>Registers the glob tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers the glob tool with validated host ceilings.</summary>
        /// <param name="configure">Optional traversal-bound configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddGlobTool(Action<GlobToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<GlobToolOptions>()
                .Validate(static value => value.DefaultMaximumDepth > 0, "DefaultMaximumDepth must be positive.")
                .Validate(static value => value.MaximumDepth > 0, "MaximumDepth must be positive.")
                .Validate(
                    static value => value.DefaultMaximumDepth <= value.MaximumDepth,
                    "DefaultMaximumDepth must not exceed MaximumDepth.")
                .Validate(
                    static value => value.DefaultMaximumVisitedEntries > 0,
                    "DefaultMaximumVisitedEntries must be positive.")
                .Validate(
                    static value => value.MaximumVisitedEntries > 0,
                    "MaximumVisitedEntries must be positive.")
                .Validate(
                    static value => value.DefaultMaximumVisitedEntries <= value.MaximumVisitedEntries,
                    "DefaultMaximumVisitedEntries must not exceed MaximumVisitedEntries.")
                .Validate(static value => value.DefaultMaximumResults > 0, "DefaultMaximumResults must be positive.")
                .Validate(static value => value.MaximumResults > 0, "MaximumResults must be positive.")
                .Validate(
                    static value => value.DefaultMaximumResults <= value.MaximumResults,
                    "DefaultMaximumResults must not exceed MaximumResults.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, GlobTool>());
            return services;
        }
    }
}
