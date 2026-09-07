// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Marks one object-in/object-out method as an MCP tool contract.</summary>
/// <remarks>
/// The declaring class is the tool surface. Reflection derives the request
/// parameter name and request/response CLR types; this attribute supplies only
/// stable protocol identity, contract version, and effect hints.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class McpToolAttribute: Attribute
{
    /// <summary>Initializes MCP tool metadata.</summary>
    /// <param name="name">The stable protocol-facing tool name.</param>
    /// <param name="version">The version of this tool's request, response, and behavior contract.</param>
    /// <exception cref="ArgumentException">Either argument is null, empty, or whitespace.</exception>
    public McpToolAttribute(string name, string version)
    {
        Name = new McpToolName(name);
        Version = new ToolVersion(version);
    }

    /// <summary>Gets the stable protocol-facing tool name.</summary>
    public McpToolName Name { get; }

    /// <summary>Gets the tool contract version, independent of the MCP protocol revision.</summary>
    public ToolVersion Version { get; }

    /// <summary>Gets or sets whether the method does not modify its environment.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Gets or sets whether repeated calls with equal inputs have no additional effect.</summary>
    public bool Idempotent { get; set; }

    /// <summary>Gets or sets whether the method can interact with an unpredictable external world.</summary>
    public bool OpenWorld { get; set; } = true;

    /// <summary>Gets or sets whether the method may perform destructive updates.</summary>
    public bool Destructive { get; set; } = true;
}
