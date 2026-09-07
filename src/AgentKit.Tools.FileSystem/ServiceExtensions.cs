// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.FileSystem;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration for the file-system tools.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Additively registers <see cref="ReadFileTool"/> and
        /// <see cref="WriteFileTool"/>.
        /// </summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// This method requires an <see cref="IFileSystem"/> to already be
        /// registered (for example through <c>AddSandboxedFileSystem</c>);
        /// it does not register one itself. Registering these tools alone
        /// does not authorize them: an application must still add
        /// <see cref="ReadFileTool.Id"/> and <see cref="WriteFileTool.Id"/>
        /// to its configured tool allow-list before either can be invoked.
        /// </remarks>
        public IServiceCollection AddFileSystemTools()
        {
            _ = services.AddTool<ReadFileTool>();
            _ = services.AddTool<WriteFileTool>();
            return services;
        }
    }
}
