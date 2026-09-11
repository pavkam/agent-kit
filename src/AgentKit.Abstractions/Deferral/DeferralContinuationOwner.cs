// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the owner whose durable state permits continuation of deferred work.</summary>
public enum DeferralContinuationOwner
{
    /// <summary>The original operation stays open and yields a nonterminal wait.</summary>
    RuntimeOperation,
    /// <summary>A durable external handoff permits the current run to finish; resolution starts a causally linked run.</summary>
    ExternalWorkflow,
}
