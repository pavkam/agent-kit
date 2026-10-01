# AgentKit.Goals.Hosting

The host worker that runs delegated children. A child never runs inside the
delegating call: `LocalDelegationDispatcher` (in `AgentKit.Goals`) commits a
durable, ready child goal, and this package's `GoalDelegationWorker` claims and
runs it through the public `AgentEngine` surface.

```csharp
services.AddAgentGoals();
services.AddInMemoryGoalStore(new GoalStoreKey("memory"));
services.AddLocalDelegationDispatcher(new DelegationDispatcherKey("local"));
services.AddGoalDelegationWorker(options =>
{
    options.MaximumConcurrentChildren = 4;
    options.Profiles.Add(new GoalProfileReference(new GoalProfileKey("default"), new GoalProfileVersion(1)));
});
```

## Behavior

- **Claim before run.** The worker records the running attempt with one atomic
  transition before anything executes, so a duplicated signal, a rescan, or a
  second worker finds the child already active and never starts a second run.
- **Signals are hints.** The in-process queue is bounded and lossy; durable
  state is truth. On startup and every `ScanInterval` the worker scans the
  durable intents of the configured profiles, which recovers work after process
  loss. A store that cannot discover intents is reached only by the signal.
- **Process loss.** A running attempt left by a previous incarnation is settled
  as failed with unknown side effects. It is not silently rerun, because the
  effects of the lost run are unknown; retrying is an explicit new attempt.
- **Deadline.** The delegation deadline cancels the run and settles the attempt.
  A stopping worker settles a claimed attempt before it exits.
- **Parking.** A child that delegates and waits releases its slot for the wait
  and reacquires one afterward, so one slot is enough for a chain of nested
  delegations.
- **Engine-backed runner.** `EngineDelegationChildRunner` provisions a session
  owned by the delegating identity and runs the target agent with
  `Agent.StreamAsync`. It needs a subscribable output publisher; replace it with
  `ReplaceDelegationChildRunner<T>()` to run children elsewhere.

The worker resolves the engine lazily, so it never constructs the engine during
composition. All mutations go through `IGoalCoordinator` under the delegation's
captured authorization; the worker never widens authority or writes a store
directly.
