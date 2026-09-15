// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

/// <summary>Provides dependency-injection registration for the read-file tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers <see cref="ReadFileTool"/> as an available AgentKit tool and validates its line-window configuration.</summary>
        /// <param name="configure">
        /// Optional configuration for <see cref="ReadFileToolOptions"/>. When omitted, the documented
        /// defaults apply: a 2,000-line window for an omitted <c>limit</c> and a 20,000-line ceiling.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// <para>
        /// Registration uses <c>TryAddEnumerable</c>: other <see cref="ITool"/>
        /// implementations are preserved, while repeated calls register exactly one
        /// <see cref="ReadFileTool"/> implementation. Each call adds its
        /// <paramref name="configure"/> delegate to the options pipeline, so repeated calls
        /// compose configuration in registration order. This method does not register
        /// <see cref="IFileSystem"/> or grant authority to read files; applications
        /// must provide both independently.
        /// </para>
        /// <para>
        /// Options validation requires both line bounds to be positive and
        /// <see cref="ReadFileToolOptions.DefaultMaximumLines"/> to be no greater than
        /// <see cref="ReadFileToolOptions.MaximumLines"/>. Validation runs on host start and
        /// again whenever the options value is first resolved, surfacing an
        /// <see cref="OptionsValidationException"/> at the composition boundary.
        /// </para>
        /// </remarks>
        public IServiceCollection AddReadTool(Action<ReadFileToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<ReadFileToolOptions>()
                .Validate(static value => value.DefaultMaximumLines > 0, "DefaultMaximumLines must be positive.")
                .Validate(static value => value.MaximumLines > 0, "MaximumLines must be positive.")
                .Validate(
                    static value => value.DefaultMaximumLines <= value.MaximumLines,
                    "DefaultMaximumLines must not exceed MaximumLines.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ReadFileTool>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolPresentationFormatter, ReadFileToolPresentationFormatter>());
            return services;
        }
    }
}
