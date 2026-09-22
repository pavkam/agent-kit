// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Language;

using AgentKit.Tools;

/// <summary>Registers the read-only language-intelligence tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers one language tool and validates its model-facing bounds.</summary>
        /// <param name="configure">Optional bound configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddLanguageTool(Action<LanguageToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<LanguageToolOptions>()
                .Validate(
                    static value => value.DefaultMaximumResults > 0
                        && value.DefaultMaximumResults <= value.MaximumResults,
                    "Result bounds are invalid.")
                .Validate(
                    static value => value.DefaultTimeout > TimeSpan.Zero
                        && value.DefaultTimeout <= value.MaximumTimeout,
                    "Timeout bounds are invalid.")
                .Validate(static value => value.MaximumQueryCharacters > 0, "MaximumQueryCharacters must be positive.")
                .Validate(static value => value.MaximumTextCharacters > 0, "MaximumTextCharacters must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            _ = services.AddToolInvoker<LanguageTool>(LanguageTool.Descriptor);
            if (!services.Any(static descriptor =>
                    descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(ToolsetPublication)
                    && descriptor.ServiceKey is ToolsetKey key
                    && key == LanguageTool.DefaultToolset.Key))
            {
                _ = services.AddToolset(LanguageTool.DefaultToolset);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, LanguageTool>());
            return services;
        }
    }
}
