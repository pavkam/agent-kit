// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Endpoint resolution failed for a typed catalog reason.</summary>
public sealed record McpEndpointResolutionFailed: McpEndpointResolution
{
    /// <summary>Initializes a failed endpoint resolution.</summary>
    /// <param name="key">The requested endpoint key.</param>
    /// <param name="safeMessage">A non-sensitive failure summary.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> or <paramref name="safeMessage"/> is uninitialized.</exception>
    public McpEndpointResolutionFailed(McpEndpointKey key, string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage, nameof(safeMessage));
        Key = key;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the requested endpoint key.</summary>
    public McpEndpointKey Key { get; }

    /// <summary>Gets the non-sensitive failure summary.</summary>
    public string SafeMessage { get; }
}
