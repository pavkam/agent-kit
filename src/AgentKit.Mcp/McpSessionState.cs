// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Describes where one MCP client session is in its lifecycle.</summary>
/// <remarks>
/// Sessions move forward monotonically until they reach
/// <see cref="Faulted"/> or <see cref="Disposed"/>. A session in
/// <see cref="Ready"/> may still reject requests when a grant, capability, or
/// transport constraint fails at invocation time.
/// </remarks>
public enum McpSessionState
{
    /// <summary>The session object exists but initialization has not started.</summary>
    Created = 0,

    /// <summary>Protocol initialization or discovery is in progress.</summary>
    Initializing = 1,

    /// <summary>The session completed initialization and may serve correlated requests.</summary>
    Ready = 2,

    /// <summary>The session encountered an unrecoverable protocol or transport fault.</summary>
    Faulted = 3,

    /// <summary>The session is disposed and rejects new work.</summary>
    Disposed = 4,
}
