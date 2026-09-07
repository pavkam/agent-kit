// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Server;

/// <summary>Reflects AgentKit tool classes into official MCP server builders.</summary>
public static class McpServerBuilderExtensions
{
    extension(IMcpServerBuilder builder)
    {
        /// <summary>
        /// Reflects every <see cref="McpToolAttribute"/> method on <typeparamref name="TTools"/> into an MCP
        /// tool schema and invocation handler.
        /// </summary>
        /// <typeparam name="TTools">
        /// A concrete tool class with one request object and one asynchronous response object per attributed method.
        /// Dependencies are resolved from the official SDK request scope for a fresh tool instance per invocation.
        /// </typeparam>
        /// <param name="serializerOptions">Optional caller-owned JSON settings copied for schema generation and invocation binding.</param>
        /// <returns>The same MCP server builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><typeparamref name="TTools"/> is abstract or has an invalid reflected contract.</exception>
        /// <remarks>
        /// The SDK owns JSON-RPC framing and request correlation. Reflection supplies the <c>tools/list</c> schemas,
        /// the <c>tools/call</c> argument member, structured output schema, and invocation binding. Tool contract
        /// version is published separately in <see cref="McpMetadataKeys.ToolContractVersion"/>.
        /// </remarks>
        public IMcpServerBuilder WithAgentKitTools<TTools>(JsonSerializerOptions? serializerOptions = null)
            where TTools : class
        {
            ArgumentNullException.ThrowIfNull(builder);
            if (typeof(TTools).IsAbstract)
            {
                throw new InvalidOperationException($"MCP server tool class '{typeof(TTools)}' must be concrete.");
            }

            var contract = new McpToolContract<TTools>();
            JsonSerializerOptions? effectiveSerializerOptions = null;
            if (serializerOptions is not null)
            {
                effectiveSerializerOptions = new JsonSerializerOptions(serializerOptions);
                effectiveSerializerOptions.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
            }

            var tools = contract.Methods.Select(descriptor => McpServerTool.Create(
                descriptor.Method,
                context => ActivatorUtilities.CreateInstance<TTools>(
                    context.Services ??
                    throw new InvalidOperationException("The MCP request did not provide a dependency-injection scope.")),
                new McpServerToolCreateOptions
                {
                    Name = descriptor.Name.Value,
                    Description = descriptor.Description,
                    ReadOnly = descriptor.ReadOnly,
                    Idempotent = descriptor.Idempotent,
                    OpenWorld = descriptor.OpenWorld,
                    Destructive = descriptor.Destructive,
                    UseStructuredContent = true,
                    SerializerOptions = effectiveSerializerOptions,
                    Meta = new JsonObject
                    {
                        [McpMetadataKeys.ToolContractVersion] = descriptor.Version.Value
                    }
                })).ToArray();

            return builder.WithTools(tools);
        }
    }
}
