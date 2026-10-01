// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

/// <summary>Configures the host worker that drains committed delegation intents.</summary>
/// <remarks>Every bound is validated when the worker is composed, so an impossible value fails at startup instead of mid-run. The defaults are documented and replaceable; the worker never chooses a store or a profile for the application.</remarks>
public sealed class GoalWorkerOptions
{
    /// <summary>Gets or sets the number of child attempts the worker runs at the same time.</summary>
    /// <value>4 by default; must be positive. A parent that waits on its child releases its slot while it waits, so one slot is enough for a chain of nested delegations.</value>
    public int MaximumConcurrentChildren { get; set; } = 4;

    /// <summary>Gets or sets the capacity of the in-process wake-up queue.</summary>
    /// <value>256 by default; must be positive. A full queue drops the hint, never the intent: the periodic scan rediscovers it from durable state.</value>
    public int QueueCapacity { get; set; } = 256;

    /// <summary>Gets or sets how often the worker rescans durable intents for the configured profiles.</summary>
    /// <value>30 seconds by default; must be positive. The scan is what recovers intents after process loss or a dropped signal.</value>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the page size used while scanning durable intents.</summary>
    /// <value>50 by default; must be positive.</value>
    public int ScanPageSize { get; set; } = 50;

    /// <summary>Gets or sets the identity the host configured to scan intents across tenants.</summary>
    /// <value><c>agentkit.goals.worker</c> by default. The store authorizes the scan by this configured identity rather than by a grant, so it must be a value the host chose to trust.</value>
    public ComponentId ScannerId { get; set; } = new("agentkit.goals.worker");

    /// <summary>Gets the goal profiles whose durable intents the worker scans at startup and periodically.</summary>
    /// <value>Empty by default, which drains only signalled intents. A store that cannot discover intents is reached only through the signal.</value>
    public List<GoalProfileReference> Profiles { get; } = [];

    /// <summary>Gets or sets the largest answer, in characters, the runner returns for a succeeded child.</summary>
    /// <value>16,000 by default; must be positive. A longer answer is truncated, never rejected.</value>
    public int MaximumSummaryCharacters { get; set; } = 16_000;
}
