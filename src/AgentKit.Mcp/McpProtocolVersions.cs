// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Provides the MCP revisions supported by the pinned first-party SDK.</summary>
public static class McpProtocolVersions
{
    /// <summary>Gets protocol revision 2024-11-05.</summary>
    public static McpProtocolVersion November2024 { get; } = new(new DateOnly(2024, 11, 5));

    /// <summary>Gets protocol revision 2025-03-26.</summary>
    public static McpProtocolVersion March2025 { get; } = new(new DateOnly(2025, 3, 26));

    /// <summary>Gets protocol revision 2025-06-18.</summary>
    public static McpProtocolVersion June2025 { get; } = new(new DateOnly(2025, 6, 18));

    /// <summary>Gets protocol revision 2025-11-25, the latest legacy revision.</summary>
    public static McpProtocolVersion November2025 { get; } = new(new DateOnly(2025, 11, 25));

    /// <summary>Gets protocol revision 2026-07-28, the first modern revision.</summary>
    public static McpProtocolVersion July2026 { get; } = new(new DateOnly(2026, 7, 28));

    /// <summary>Gets every protocol revision supported by ModelContextProtocol 2.2.0.</summary>
    public static ImmutableArray<McpProtocolVersion> SupportedBySdk { get; } =
        [November2024, March2025, June2025, November2025, July2026];
}
