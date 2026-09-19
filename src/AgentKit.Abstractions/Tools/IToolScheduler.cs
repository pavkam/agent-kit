// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Executes one preflighted <see cref="ToolBatch"/> under deterministic scheduling and publication order.</summary>
/// <remarks>
/// The scheduler receives already prepared invoker handles from the resolver; it never resolves an
/// <see cref="IServiceProvider"/> or reimplements resolution, validation, or authorization. Under the barrier-
/// segment algorithm, calls partition into parallel-safe segments separated by sequential barriers; a
/// concurrency-key conflict creates an additional deterministic sub-barrier. Completion order may differ from the
/// batch's source order, but every accepted entry reaches exactly one terminal result. On cancellation the
/// scheduler stops admitting new segments, signals running invocations, awaits them within a bounded drain
/// deadline, and marks unsettled calls interrupted; synchronous or remote side effects may continue and their
/// result reports outcome unknown rather than a claimed rollback.
/// </remarks>
public interface IToolScheduler
{
    /// <summary>Executes every entry in <paramref name="batch"/> under this scheduler's ordering and concurrency policy.</summary>
    /// <param name="batch">The complete preflighted batch to execute.</param>
    /// <param name="cancellationToken">Signals cancellation; already-started effects still settle to a terminal result.</param>
    /// <returns>A task producing one terminal result per accepted entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="batch"/> is null.</exception>
    public Task<ToolBatchResult> ExecuteAsync(ToolBatch batch, CancellationToken cancellationToken = default);
}
