// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>One immutable MCP catalog generation for a session.</summary>
/// <remarks>
/// Snapshots are replaced atomically when list-change notifications arrive.
/// In-flight model requests keep the snapshot they originally observed.
/// </remarks>
public sealed record McpCatalogSnapshot
{
    /// <summary>Initializes an immutable catalog snapshot.</summary>
    /// <param name="sessionId">The owning session identity.</param>
    /// <param name="version">The positive catalog generation.</param>
    /// <param name="tools">Tool descriptors advertised by the server.</param>
    /// <param name="resources">Resource descriptors advertised by the server.</param>
    /// <param name="prompts">Prompt descriptors advertised by the server.</param>
    /// <param name="capabilities">The negotiated capability view captured with this snapshot.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sessionId"/> or <paramref name="version"/> is default or not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="tools"/>, <paramref name="resources"/>, or <paramref name="prompts"/> is default.
    /// </exception>
    public McpCatalogSnapshot(
        McpSessionId sessionId,
        McpCatalogVersion version,
        ImmutableArray<ToolDescriptor> tools,
        ImmutableArray<McpResourceDescriptor> resources,
        ImmutableArray<McpPromptDescriptor> prompts,
        McpCapabilitySet capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfLessThan(version.Value, 1, nameof(version));
        ArgumentException.ThrowIfDefault(tools);
        ArgumentException.ThrowIfDefault(resources);
        ArgumentException.ThrowIfDefault(prompts);
        ArgumentNullException.ThrowIfNull(capabilities);

        SessionId = sessionId;
        Version = version;
        Tools = tools;
        Resources = resources;
        Prompts = prompts;
        Capabilities = capabilities;
    }

    /// <summary>Gets the owning session identity.</summary>
    public McpSessionId SessionId { get; }

    /// <summary>Gets the catalog generation.</summary>
    public McpCatalogVersion Version { get; }

    /// <summary>Gets tool descriptors advertised by the server.</summary>
    public ImmutableArray<ToolDescriptor> Tools { get; }

    /// <summary>Gets resource descriptors advertised by the server.</summary>
    public ImmutableArray<McpResourceDescriptor> Resources { get; }

    /// <summary>Gets prompt descriptors advertised by the server.</summary>
    public ImmutableArray<McpPromptDescriptor> Prompts { get; }

    /// <summary>Gets the negotiated capability view.</summary>
    public McpCapabilitySet Capabilities { get; }
}
