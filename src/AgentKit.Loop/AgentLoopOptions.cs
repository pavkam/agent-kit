// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Options controlling <see cref="DefaultAgentLoop"/> behavior.</summary>
/// <remarks>
/// Every value is validated when the options are first resolved (see <c>AddAgentLoop</c>), so an impossible
/// limit fails at composition rather than in the middle of a run. All timeouts are measured with the injected
/// <see cref="TimeProvider"/>.
/// </remarks>
public sealed class AgentLoopOptions
{
    /// <summary>
    /// Gets or sets the maximum number of entries requested per
    /// <see cref="ISessionCoordinator.ReadAsync"/> page while loading a
    /// run's eligible history. Defaults to 200.
    /// </summary>
    /// <value>A positive page size.</value>
    public int HistoryReadPageSize { get; set; } = 200;

    /// <summary>
    /// Gets or sets how many times one append is retried, rebased onto the actual branch tip, after a concurrent
    /// writer advanced the branch. Defaults to 5.
    /// </summary>
    /// <value>
    /// A non-negative retry count. Zero disables rebasing: the first conflict settles the run as a session
    /// operation failure. The bound turns a pathological runaway writer into a clear failure instead of an
    /// unbounded loop; an ordinary interleaved tool append settles on the first retry.
    /// </value>
    public int AppendConflictRetryLimit { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether the final permitted turn of a run is requested with no tools and
    /// <see cref="LlmToolChoice.None"/>, so the model produces its final response instead of requesting calls
    /// the loop can no longer invoke. Defaults to <see langword="true"/>.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to disable tools on the turn numbered <c>MaxTurns</c>; <see langword="false"/> to
    /// offer the run's tools on every turn, in which case calls requested on the final turn are settled with
    /// rejected terminal results and the run reports the typed turn limit. A model that requests calls despite
    /// tools being disabled is settled the same way.
    /// </value>
    public bool DisableToolsOnFinalTurn { get; set; } = true;

    /// <summary>
    /// Gets or sets the bound on each required terminal commit that must land regardless of the caller's
    /// cancellation: the tool message settling an already-committed assistant request, and the interrupted
    /// message preserving partial model output. Defaults to 30 seconds.
    /// </summary>
    /// <value>
    /// A positive duration. When the bound elapses the run settles as a session operation failure whose message
    /// states that the commit outcome is unknown; the loop never hangs on settlement.
    /// </value>
    public TimeSpan SettlementTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the bound on delivering one run event to the request's <see cref="IAgentRunObserver"/> when
    /// the delivery must not use the caller's (possibly already cancelled) token, such as terminal tool results
    /// of an interrupted batch. Defaults to 5 seconds.
    /// </summary>
    /// <value>A positive duration. Delivery that exceeds it is dropped and logged like any other observer failure.</value>
    public TimeSpan ObserverDeliveryTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the fraction of the selected model's context window at which the loop asks the composed
    /// <see cref="ICompactor"/> to checkpoint older history before sending the next request.
    /// </summary>
    /// <value>
    /// A value in the open interval (0, 1]; 0.8 by default. Applies only when an <see cref="ICompactor"/> is composed
    /// and the model declares <see cref="ModelLimits.MaxContextTokens"/>; otherwise no pressure trigger runs.
    /// </value>
    public double ContextPressureThreshold { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the characters-per-token ratio the loop uses to estimate the size of the history it is about to
    /// send when deciding whether to trigger compaction.
    /// </summary>
    /// <value>A positive ratio; 4.0 by default. Estimation is advisory and never blocks a request on its own.</value>
    public double EstimatedCharactersPerToken { get; set; } = 4.0;
}
