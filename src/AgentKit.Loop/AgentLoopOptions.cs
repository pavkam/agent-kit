// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Options controlling <see cref="DefaultAgentLoop"/> behavior.</summary>
public sealed class AgentLoopOptions
{
    /// <summary>
    /// Gets or sets the maximum number of entries requested per
    /// <see cref="ISessionCoordinator.ReadAsync"/> page while loading a
    /// run's eligible history. Defaults to 200.
    /// </summary>
    public int HistoryReadPageSize { get; set; } = 200;
}
