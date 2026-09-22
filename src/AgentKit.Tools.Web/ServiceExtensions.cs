// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Web;

using AgentKit.Tools;

/// <summary>Registers the bounded web-fetch tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers web fetch and validates every model-facing bound.</summary>
        /// <param name="configure">Optional host bound configuration.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddWebFetchTool(Action<WebFetchToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<WebFetchToolOptions>()
                .Validate(static value => value.DefaultMaximumCharacters > 0
                    && value.DefaultMaximumCharacters <= value.MaximumCharacters, "Character bounds are invalid.")
                .Validate(static value => value.MaximumResponseBytes > 0, "MaximumResponseBytes must be positive.")
                .Validate(static value => value.DefaultTimeout > TimeSpan.Zero
                    && value.DefaultTimeout <= value.MaximumTimeout, "Timeout bounds are invalid.")
                .Validate(static value => value.ConnectTimeout > TimeSpan.Zero, "ConnectTimeout must be positive.")
                .Validate(static value => value.MaximumRedirects >= 0, "MaximumRedirects cannot be negative.")
                .Validate(static value => value.MaximumUrlCharacters > 0, "MaximumUrlCharacters must be positive.")
                .Validate(static value => value.MaximumHeaderCount > 0, "MaximumHeaderCount must be positive.")
                .Validate(static value => value.MaximumHeaderCharacters > 0, "MaximumHeaderCharacters must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            _ = services.AddToolInvoker<WebFetchTool>(WebFetchTool.Descriptor);
            if (!services.Any(static descriptor =>
                    descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(ToolsetPublication)
                    && descriptor.ServiceKey is ToolsetKey key
                    && key == WebFetchTool.DefaultToolset.Key))
            {
                _ = services.AddToolset(WebFetchTool.DefaultToolset);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, WebFetchTool>());
            return services;
        }
    }
}
