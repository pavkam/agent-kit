// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Reports that a remote MCP catalog cannot satisfy a reflected local tool contract.</summary>
[Serializable]
public sealed class McpToolContractMismatchException: Exception
{
    /// <summary>Initializes a mismatch exception with a safe diagnostic message.</summary>
    /// <param name="message">The non-empty mismatch description.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is null, empty, or whitespace.</exception>
    public McpToolContractMismatchException(string message)
        : base(message) => ArgumentException.ThrowIfNullOrWhiteSpace(message);
}
