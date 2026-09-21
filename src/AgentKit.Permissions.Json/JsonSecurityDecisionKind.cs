// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Discriminates the three closed terminal security decision shapes persisted by the JSON decision store.</summary>
public enum JsonSecurityDecisionKind
{
    /// <summary>The decision issued a bounded grant.</summary>
    Allowed = 0,

    /// <summary>The decision denied the request without authority.</summary>
    Denied = 1,

    /// <summary>The decision deferred to durable human approval.</summary>
    ApprovalRequired = 2,
}
