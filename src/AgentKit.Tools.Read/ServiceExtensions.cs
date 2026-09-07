// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Provides dependency-injection registration for the read-file tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers <see cref="ReadFileTool"/> as an available AgentKit tool.</summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Registration uses <c>TryAddEnumerable</c>: other <see cref="ITool"/>
        /// implementations are preserved, while repeated calls register exactly one
        /// <see cref="ReadFileTool"/> implementation. This method does not register
        /// <see cref="IFileSystem"/> or grant authority to read files; applications
        /// must provide both independently.
        /// </remarks>
        public IServiceCollection AddReadTool()
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ReadFileTool>());
            return services;
        }
    }
}
