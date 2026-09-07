// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>
/// Identifies the wire-lifecycle family to which an MCP protocol revision
/// belongs.
/// </summary>
/// <remarks>
/// Legacy revisions establish negotiated state through <c>initialize</c>.
/// Modern revisions beginning with 2026-07-28 carry protocol identity and
/// capabilities on each request and do not establish a protocol session.
/// </remarks>
public enum McpProtocolEra
{
    /// <summary>The initialization-handshake lifecycle used through revision 2025-11-25.</summary>
    Legacy,

    /// <summary>The per-request metadata lifecycle introduced by revision 2026-07-28.</summary>
    Modern
}
