// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Transport open failed for a typed reason other than denial.</summary>
public sealed record McpTransportOpenFailed: McpTransportOpenResult
{
    /// <summary>Initializes a transport failure.</summary>
    /// <param name="safeMessage">A non-sensitive failure summary.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is uninitialized.</exception>
    public McpTransportOpenFailed(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage, nameof(safeMessage));
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the non-sensitive failure summary.</summary>
    public string SafeMessage { get; }
}
