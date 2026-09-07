// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Pairs an authorized child-work envelope with the exact single-use delegation grant.</summary>
public sealed record TaskDelegationRequest
{
    /// <summary>Initializes an authorized delegation request.</summary>
    /// <param name="prompt">The validated child-work envelope.</param>
    /// <param name="grant">The exact grant the broker must consume before dispatch.</param>
    /// <exception cref="ArgumentNullException">A value is null.</exception>
    public TaskDelegationRequest(TaskDelegationPrompt prompt, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(grant);
        Prompt = prompt;
        Grant = grant;
    }

    /// <summary>Gets the validated child-work envelope.</summary>
    public TaskDelegationPrompt Prompt { get; init; }
    /// <summary>Gets the exact single-use delegation grant.</summary>
    public SecurityGrant Grant { get; init; }
}
