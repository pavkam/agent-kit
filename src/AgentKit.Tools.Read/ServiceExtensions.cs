// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

using AgentKit.Tools;

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
        /// Registers the spec-shaped <see cref="IToolInvoker"/>, publishes <see cref="ReadFileTool.DefaultToolset"/>,
        /// and retains legacy <see cref="ITool"/> registration until workstream 4 chunk C10.
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
                .Validate(
                    static value => !string.IsNullOrWhiteSpace(value.HostRootPath),
                    "HostRootPath must be configured.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<IIdentifierGenerator<FileOperationId>, GuidFileOperationIdGenerator>();
            services.TryAddSingleton<IFilePathNormalizer, ReadToolPathNormalizer>();
            _ = services.AddToolInvoker<ReadFileTool>(ReadFileTool.Descriptor);
            if (!services.Any(static descriptor =>
                    descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(ToolsetPublication)
                    && descriptor.ServiceKey is ToolsetKey key
                    && key == ReadFileTool.DefaultToolset.Key))
            {
                _ = services.AddToolset(ReadFileTool.DefaultToolset);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ReadFileTool>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolPresentationFormatter, ReadFileToolPresentationFormatter>());
            return services;
        }
    }
}
