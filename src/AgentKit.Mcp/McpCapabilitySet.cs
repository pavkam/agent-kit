// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Captures negotiated MCP capability flags for one session snapshot.</summary>
public sealed record McpCapabilitySet
{
    /// <summary>Initializes a capability set.</summary>
    /// <param name="tools">Whether tool list and call operations are negotiated.</param>
    /// <param name="resources">Whether resource operations are negotiated.</param>
    /// <param name="prompts">Whether prompt operations are negotiated.</param>
    /// <param name="roots">Whether filesystem root operations are negotiated.</param>
    /// <param name="sampling">Whether reverse sampling is negotiated.</param>
    /// <param name="elicitation">Whether reverse elicitation is negotiated.</param>
    /// <param name="logging">Whether logging notifications are negotiated.</param>
    public McpCapabilitySet(
        bool tools = false,
        bool resources = false,
        bool prompts = false,
        bool roots = false,
        bool sampling = false,
        bool elicitation = false,
        bool logging = false)
    {
        Tools = tools;
        Resources = resources;
        Prompts = prompts;
        Roots = roots;
        Sampling = sampling;
        Elicitation = elicitation;
        Logging = logging;
    }

    /// <summary>Gets whether tool operations are negotiated.</summary>
    public bool Tools { get; }

    /// <summary>Gets whether resource operations are negotiated.</summary>
    public bool Resources { get; }

    /// <summary>Gets whether prompt operations are negotiated.</summary>
    public bool Prompts { get; }

    /// <summary>Gets whether root operations are negotiated.</summary>
    public bool Roots { get; }

    /// <summary>Gets whether reverse sampling is negotiated.</summary>
    public bool Sampling { get; }

    /// <summary>Gets whether reverse elicitation is negotiated.</summary>
    public bool Elicitation { get; }

    /// <summary>Gets whether logging notifications are negotiated.</summary>
    public bool Logging { get; }
}
