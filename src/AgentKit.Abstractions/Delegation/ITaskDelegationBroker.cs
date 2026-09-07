// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Protects dispatch of scoped child goals by consuming exact delegation authority.</summary>
public interface ITaskDelegationBroker
{
    /// <summary>Gets the security audience that enforces dispatch.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Consumes authority and delegates one bounded child goal.</summary>
    /// <param name="request">The authorized request.</param>
    /// <param name="cancellationToken">Cancels waiting and must settle or hand off admitted child work.</param>
    /// <returns>The typed terminal result.</returns>
    public ValueTask<TaskDelegationResult> DelegateAsync(TaskDelegationRequest request, CancellationToken cancellationToken = default);
}
