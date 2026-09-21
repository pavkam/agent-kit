// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names how the approval broker behaves when no inline handler resolves a durable request.</summary>
public enum HeadlessApprovalBehavior
{
    /// <summary>Fail closed when no inline handler produces a terminal response.</summary>
    Deny = 0,

    /// <summary>Retain the durable request and return a deferred broker outcome when storage is durable.</summary>
    Defer = 1,
}
