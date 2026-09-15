// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write;

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
        /// Registration uses <c>TryAddEnumerable</c>: other <see cref="ITool"/>
        /// implementations are preserved, while repeated calls register exactly one
        /// <see cref="WriteFileTool"/> implementation. This method does not register
        /// <see cref="IFileSystem"/> or grant authority to write files; applications
        /// must provide both independently.
        /// </remarks>
        public IServiceCollection AddWriteTool()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, WriteFileTool>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolPresentationFormatter, WriteFileToolPresentationFormatter>());
            return services;
        }
    }
}
