// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Reports a remote tool failure or an unusable structured result.</summary>
[Serializable]
public sealed class McpToolInvocationException: Exception
{
    /// <summary>Initializes a tool invocation exception.</summary>
    /// <param name="toolName">The remote tool that failed.</param>
    /// <param name="message">The safe failure description.</param>
    /// <exception cref="ArgumentException"><paramref name="toolName"/> is uninitialized, or <paramref name="message"/> is null, empty, or whitespace.</exception>
    public McpToolInvocationException(McpToolName toolName, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName.Value, nameof(toolName));
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ToolName = toolName;
    }

    /// <summary>Gets the remote tool that failed.</summary>
    public McpToolName ToolName { get; }
}
