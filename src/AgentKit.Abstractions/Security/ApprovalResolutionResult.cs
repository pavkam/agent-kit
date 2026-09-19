// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Represents the closed terminal result of resolving one durable approval request through the approval broker's durable resolution path.</summary>
public abstract record ApprovalResolutionResult
{
    /// <summary>Initializes base state for a resolution-result type.</summary>
    private protected ApprovalResolutionResult()
    {
    }
}
