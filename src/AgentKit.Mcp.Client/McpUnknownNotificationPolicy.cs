// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Controls how a client session reacts to unknown inbound notifications.</summary>
public enum McpUnknownNotificationPolicy
{
    /// <summary>Ignore unknown notifications while recording diagnostics.</summary>
    IgnoreAndDiagnose = 0,

    /// <summary>Fail the session when an unknown required notification arrives.</summary>
    FailSession = 1,
}
