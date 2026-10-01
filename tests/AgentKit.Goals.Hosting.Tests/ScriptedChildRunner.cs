// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting.Tests;

/// <summary>Scripts a child runner so worker tests control provisioning, execution, and timing deterministically.</summary>
internal sealed class ScriptedChildRunner: IDelegationChildRunner
{
    private int _runs;

    internal ConcurrentQueue<DelegationChildRunRequest> Requests { get; } = [];

    internal ConcurrentQueue<(GoalId Goal, int Attempt)> Provisions { get; } = [];

    internal int Runs => Volatile.Read(ref _runs);

    internal bool Provisionable { get; set; } = true;

    internal Func<DelegationChildRunRequest, CancellationToken, Task<DelegationChildRunResult>> Behavior { get; set; } =
        static (_, _) => Task.FromResult(new DelegationChildRunResult(new RunId(Guid.NewGuid()), DelegationStatus.Succeeded, "Child answer.", new GoalBudgetUsage(1, 0, 0), SideEffectCertainty.DefinitelyNotPerformed));

    public ValueTask<SessionId?> ProvisionSessionAsync(DelegationRequest delegation, GoalId childGoalId, int attemptNumber, CancellationToken cancellationToken = default)
    {
        Provisions.Enqueue((childGoalId, attemptNumber));
        return ValueTask.FromResult<SessionId?>(Provisionable ? new SessionId(Guid.NewGuid()) : null);
    }

    public async ValueTask<DelegationChildRunResult> RunAsync(DelegationChildRunRequest request, CancellationToken cancellationToken = default)
    {
        _ = Interlocked.Increment(ref _runs);
        Requests.Enqueue(request);
        return await Behavior(request, cancellationToken);
    }
}
