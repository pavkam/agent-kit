// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Transport open was denied before streams or frames were created.</summary>
public sealed record McpTransportOpenDenied: McpTransportOpenResult
{
    /// <summary>Initializes a transport denial.</summary>
    /// <param name="safeMessage">A non-sensitive denial summary.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is uninitialized.</exception>
    public McpTransportOpenDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage, nameof(safeMessage));
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the non-sensitive denial summary.</summary>
    public string SafeMessage { get; }
}
