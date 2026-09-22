// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write;

using AgentKit.Tools;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Provides dependency-injection registration for the write-file tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers <see cref="WriteFileTool"/> as an available AgentKit tool.</summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// Registers the spec-shaped <see cref="IToolInvoker"/>, publishes <see cref="WriteFileTool.DefaultToolset"/>,
        /// and retains legacy <see cref="ITool"/> registration until workstream 4 chunk C10.
        /// </remarks>
        public IServiceCollection AddWriteTool(Action<WriteFileToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<WriteFileToolOptions>()
                .Validate(
                    static value => !string.IsNullOrWhiteSpace(value.HostRootPath),
                    "HostRootPath must be configured.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<IFilePathNormalizer, WriteToolPathNormalizer>();
            _ = services.AddToolInvoker<WriteFileTool>(WriteFileTool.Descriptor);
            if (!services.Any(static descriptor =>
                    descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(ToolsetPublication)
                    && descriptor.ServiceKey is ToolsetKey key
                    && key == WriteFileTool.DefaultToolset.Key))
            {
                _ = services.AddToolset(WriteFileTool.DefaultToolset);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, WriteFileTool>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolPresentationFormatter, WriteFileToolPresentationFormatter>());
            return services;
        }
    }
}
