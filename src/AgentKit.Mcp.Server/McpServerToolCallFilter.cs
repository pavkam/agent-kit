// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>Wraps every SDK-dispatched <c>tools/call</c> in AgentKit server observation.</summary>
/// <remarks>The filter is observational: it returns the downstream result or rethrows the downstream exception unchanged.</remarks>
internal static class McpServerToolCallFilter
{
    private const string _unknownServerKey = "unknown";

    /// <summary>Creates the call-tool filter handler around <paramref name="next"/>.</summary>
    /// <param name="next">The next handler in the SDK pipeline.</param>
    /// <returns>A handler that observes each call and then delegates to <paramref name="next"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="next"/> is null.</exception>
    internal static McpRequestHandler<CallToolRequestParams, CallToolResult> Create(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return (context, cancellationToken) =>
        {
            var loggerFactory = context.Services?.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            var observation = new McpServerObservation(loggerFactory.CreateLogger<McpServerObservation>());
            return observation.ObserveToolCallAsync(
                context.Server.ServerOptions.ServerInfo?.Name ?? _unknownServerKey,
                context.Params?.Name ?? string.Empty,
                () => next(context, cancellationToken),
                McpServerObservation.ClassifyToolResult);
        };
    }
}
