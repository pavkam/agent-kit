# Agent runtime

**Role:** Coordinate one run without absorbing the responsibilities of its
collaborators.

AgentKit.Loop contains the first-party agentic loop. The loop contract and all
provider-neutral run values live in AgentKit.Abstractions. For each run,
AgentEngine resolves the addressed `Agent`, creates an isolated run scope, and
uses that definition's selected keyed loop. The process-level engine does not
contain privileged orchestration behavior.

The loop owns control flow: when to request input promotion, when context is
prepared, when a model is called, when tools may run, whether another turn is
required, and when the run is
[truly settled](../concepts/run-lifecycle-and-settlement.md). Session
coordination remains in AgentKit.Session.

## State and lifecycle

A run moves through the
[explicit state-machine phases](../concepts/agent-loop-state-machine.md) for
promotion of already-admitted input, turn preparation, model streaming,
assistant commit, tool recording and execution, completion, cancellation or
failure, and settlement. State transitions are observable and validated. A run
reaches exactly one terminal outcome and one settlement event.

The runtime distinguishes generation complete, turn complete, run complete, and
run settled. The normal high-level operation waits for settlement. A stream may
expose earlier activity, but it cannot claim the session is idle while required
persistence, recovery, hook, or event work is unfinished.

## Coordination

For each turn, the runtime:

1. asks the I/O coordinator to promote eligible input at a safe boundary;
2. captures the model catalog and asks the selected model selector for one
   descriptor compatible with the turn requirements and history affinity;
3. asks the context component for an immutable provider-ready request for that
   descriptor;
4. reserves budget and asks the provider request executor to perform the
   selected concrete attempt, including only safe configured retries and
   fallback decisions; a different fallback descriptor returns through context
   assembly before it is invoked;
5. validates and commits the assistant response;
6. passes accepted tool calls through the tool and permission components;
7. commits terminal tool results in deterministic order; and
8. applies continuation and stop policy.

The runtime does not admit input, publish output, select history items, select a
model, translate provider wire formats, authorize tools, or implement storage.
It sequences the components that do.

The reduced first-party loop consults the selected `IRunContinuationPolicy`
after every committed turn: after a no-tool assistant commit with an empty
cause set, and after the tool-result commit with a
`CommittedToolResultsContinuationCause`. `ContinueRun` drives another turn while
one remains and otherwise settles as the typed turn limit, because a policy
cannot widen a hard limit; `CompleteRun` and `HaltRun` settle with the proposed
outcome. The policy is resolved from the keyed registration named by
`AgentLoopDefaults.ContinuationPolicyKey`, so registering a custom policy under
that key before `AddAgentLoop` replaces the built-in decision. Until the loop
carries an explicit lane and policy snapshot, it drives one implicit lane per
branch (the lane identity is the branch identity), reports the turn number as
its operation-state revision, and names a single fixed policy version. The
reduced loop projects each tool batch into one tool-message entry; because the
committed-turn boundary requires one distinct terminal-record identity per
call, a batch of more than one call currently continues under the canonical
committed-tool-results rule without a policy call, and the loop logs that
bypass.

Every commit is guarded by the branch version the loop last observed. When a
concurrent writer advanced the branch, the loop re-reads the interleaved range
under one pinned snapshot before retrying. An assistant response was generated
against the history the turn saw, so a concurrently committed message makes it
stale: that append fails closed with a typed session-operation outcome and
nothing is committed for the turn. Tool results and interrupted partial output
settle calls or evidence that are already committed, so they rebase and land
regardless; any interleaved message is then folded, in sequence order, into the
history the next turn assembles, so the cursor never claims history a request
did not see. Non-message facts committed by a tool mid-turn rebase without
affecting message history.

## Limits and resilience

The [run budget](budgets.md) covers turns, provider requests, tokens, cost, tool
calls, retries, elapsed time, context size, queue capacity, retrieval, and
buffered output. Concurrent work reserves capacity atomically before it starts.
Limit exhaustion is a typed run outcome and preserves any truthful partial
output or side-effect certainty.

Cancellation records its source, stops new work, propagates through every
awaitable boundary, and follows the
[bounded drain and side-effect rules](../concepts/cancellation-timeouts-and-resilience.md)
through settlement. Provider retries belong above a single-attempt adapter. Tool
retries belong to the tool pipeline. Neither may repeat work when visible output
or an uncertain side effect makes repetition unsafe.

## Replacement

The loop is replaceable as a whole and may be selected independently per agent
definition. A custom loop receives the same explicit collaborators and must
preserve public lifecycle, correlation, ordering, cancellation, and
terminal-result behavior. It cannot use the dependency container as a service
locator or bypass permission and durability boundaries.

AddAgentLoop registers the first-party implementation as singular for its
component key and replaceable at that key. Multiple keyed loops may coexist in
one engine. A custom implementation may use the neutral registration surface
without referencing AgentKit.Loop. All implementations run through the same loop
conformance suite.

## Normative minimal contract shape

These shapes reserve the minimum provider-neutral API roles. They are not an
exhaustive frozen API, and every named type is implemented in its own matching
file. The typed IDs below are the canonical values defined in
[composition and configuration](composition-and-configuration.md), not new
aliases.

```csharp
namespace AgentKit;

public sealed record AgentRunInvocation(
    AgentDefinition Definition,
    AgentCatalogVersion AgentCatalogVersion,
    SessionId SessionId,
    ExecutionLaneId ExecutionLaneId,
    ConversationId? ConversationId,
    ExecutionIdentity Identity,
    RunId RunId,
    AdmissionId? TriggerAdmissionId,
    SecurityAuthorizationContext Authorization,
    HookDispatchContext Hooks,
    AgentRunServices Services,
    EffectiveConfigurationSnapshot Configuration,
    RunPolicySnapshot Policies,
    DateTimeOffset StartedAt);

public sealed record AgentRunServices(
    IInputCoordinator Input,
    SessionExecutionCapability Session,
    IModelCatalog Models,
    IModelSelector ModelSelector,
    IContextAssembler Context,
    IModelRequestExecutor ModelExecutor,
    IToolExecutor? Tools,
    IOutputProcessor OutputProcessor,
    IOutputPublisher Output,
    IHookDispatcher Hooks,
    BudgetExecutionCapability Budget,
    IRunContinuationPolicy ContinuationPolicy);

public sealed record TurnContext(
    AgentId AgentId,
    SessionId SessionId,
    ExecutionLaneId ExecutionLaneId,
    ConversationId? ConversationId,
    RunId RunId,
    TurnId TurnId,
    long TurnNumber,
    MessageCursor History,
    EffectiveConfigurationSnapshot Configuration);

public enum AgentRunState
{
    Accepted,
    Driving,
    PromotingInput,
    PreparingTurn,
    AwaitingModel,
    StreamingModel,
    RecordingToolCalls,
    AwaitingTools,
    CommittingToolResults,
    WaitingRetry,
    SuspendedDeferred,
    Completing,
    Cancelling,
    Failing,
    Settling,
    Settled
}

public sealed record AgentRunProgress(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    RunId RunId,
    TurnId? TurnId,
    AgentRunState State,
    long EventSequence,
    RunUsage Usage);

public sealed record AgentLoopResult(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    RunId RunId,
    AgentRunOutcome Outcome,
    RunSettlementOutcome Settlement,
    MessageCursor PreviousCursor,
    ImmutableArray<AgentMessage> NewMessages,
    ValidatedOutput? Output,
    RunUsage Usage);

public interface IAgentLoop
{
    Task<AgentLoopResult> RunAsync(
        AgentRunInvocation invocation,
        CancellationToken cancellationToken = default);
}

public interface IRunContinuationPolicy
{
    ValueTask<RunContinuationDecision> DecideAsync(
        RunContinuationContext context,
        CancellationToken cancellationToken = default);
}

public abstract record RunContinuationDecision;

public sealed record ContinueRun(ContinuationReason Reason)
    : RunContinuationDecision;

public sealed record CompleteRun(AgentRunOutcome Outcome)
    : RunContinuationDecision;

public sealed record HaltRun(AgentRunOutcome Outcome)
    : RunContinuationDecision;
```

`IAgentLoop.RunAsync` returns `Task` because a run necessarily coordinates
asynchronous I/O and callers may await its terminal operation through
settlement. The continuation policy uses `ValueTask` because deterministic
policies commonly complete synchronously. Progress and results are immutable
snapshots; mutable state-machine internals stay inside the run scope.

The
[continuation evaluation contract](../concepts/agent-loop-state-machine.md#continuation-evaluation-boundary)
defines the evidence in `RunContinuationContext`, the permitted meanings of each
decision, stop precedence, and revalidation before a decision is accepted. The
context supports committed-turn, retry, and deferred-operation boundaries; it
never requires a fabricated assistant response merely to evaluate a retry.
`RunContinuationDecision` is a proposal until its expected operation state and
input boundary have been rechecked and the transition commits.

`RunId` is created once by the injected `IIdentifierGenerator<RunId>` before the
first durable run event. Each turn gets one `TurnId` from
`IIdentifierGenerator<TurnId>` and a separate monotonic turn number. Resume
restores durable IDs for an open run or turn and creates a fresh drive scope.
Continuing after a terminally deferred or otherwise settled run starts a new
`RunId` linked to the prior run and deferred operation. A new worker or drive
does not itself create a new logical run, and `RunSettled` is never reopened.

## First-party runtime class and dependencies

The default is an explicit coordinator, not a base class that custom loops must
inherit:

```csharp
namespace AgentKit.Loop;

internal sealed class DefaultAgentLoop(
    IIdentifierGenerator<TurnId> turnIdGenerator,
    TimeProvider timeProvider,
    ILogger<DefaultAgentLoop> logger) : IAgentLoop
{
}
```

The body is intentionally omitted from this constructor/dependency shape; its
observable members are exactly the `IAgentLoop` contract above. It has no
service-resolution or run-control API outside that contract.

The selected per-agent collaborators arrive through the immutable
`AgentRunInvocation.Services` bundle compiled by the run activation boundary.
Constructor injection is reserved for genuinely key-independent mechanics. This
prevents an unkeyed constructor dependency from silently replacing the
definition's selected context assembler, model path, tool executor, or
continuation policy.

No reusable loop base class is required initially: common state-transition and
settlement behavior is observable contract behavior and belongs in conformance
tests, not protected methods that force inheritance. A base class is justified
later only if two real loops share substantial lifecycle mechanics without
preventing direct `IAgentLoop` implementations.

The loop depends inward on neutral contracts. Session coordination owns durable
state, per-lane operation exclusion, and cross-lane mutation serialization; I/O
owns admission/promotion and fan-out; context owns request preparation; provider
runtime owns model selection and attempts; tools own authorization-aware
scheduling and execution. The loop sequences them and cannot replace their
decisions.

## DI, options, and replacement

```csharp
namespace AgentKit.Loop;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentLoop(
            ComponentKey<IAgentLoop> key,
            Action<AgentLoopOptions>? configure = null) =>
            AgentLoopRegistration.AddDefault(services, key, configure);

        public IServiceCollection AddAgentLoop<TLoop>(
            ComponentKey<IAgentLoop> key)
            where TLoop : class, IAgentLoop =>
            AgentLoopRegistration.Add<TLoop>(services, key);

        public IServiceCollection ReplaceAgentLoop<TLoop>(
            ComponentKey<IAgentLoop> key)
            where TLoop : class, IAgentLoop =>
            AgentLoopRegistration.Replace<TLoop>(services, key);

        public IServiceCollection AddRunContinuationPolicy<TPolicy>(
            ComponentKey<IRunContinuationPolicy> key)
            where TPolicy : class, IRunContinuationPolicy =>
            AgentLoopRegistration.AddContinuationPolicy<TPolicy>(services, key);
    }
}
```

`AddAgentLoop` uses `TryAddKeyedScoped` semantics: repeated equivalent calls are
idempotent, a conflicting implementation for the same key is diagnosed, and
`ReplaceAgentLoop` is the explicit replacement path. The default loop is
run-scoped. Its budget, mutable execution state, and selected continuation
policy are run-scoped; immutable policy snapshots may be singleton. It never
captures state from another agent or session.

Loop options and `RunPolicyDefaults` configure every behavioral choice owned by
the loop: maximum turns, final-turn behavior, tool-batch continuation, input
promotion boundaries, cancellation drain, settlement deadline, and handling of
observer or compaction failure. Safe defaults are bounded, deterministic, fail
closed on invalid state, and perform no transparent provider or tool retry. An
agent definition may select a policy key and narrow defaults; run options may
narrow them again. Managed ceilings cannot be widened by either layer.

The first-party `AgentLoopOptions` currently exposes the history read page size,
the append-conflict retry limit (rebases after a concurrent writer advanced the
branch; zero disables rebasing), whether the final permitted turn is requested
without tools and with an explicit `None` tool choice so the model produces its
final response (the default; a model that requests calls anyway has them settled
as rejected and the run reports the typed turn limit), the settlement timeout that bounds each
required terminal commit made independently of the caller's cancellation (the
tool message settling a committed assistant request and the interrupted message
preserving partial output), and the observer-delivery timeout that bounds each
event delivered outside the caller's token. All are validated at composition and
measured with the injected `TimeProvider`. When the settlement bound elapses the
run settles as a session-operation failure that states the commit outcome is
unknown; the loop never hangs on settlement and never claims a lost append
committed.

Authorization is captured fresh for the run and for every turn. When the
security authority cannot capture it, the run settles with the typed
`AgentRunAuthorizationUnavailable` outcome and attempts no protected work under
that operation; captured evidence that contradicts the run-start evidence fails
closed as invalid state. A result reports a branch version only when the loop
actually observed one: a run that settles before loading history carries no
final version rather than a fabricated zero.

Composition validates each definition's loop and continuation-policy keys, scope
graph, non-negative bounded limits, and required collaborators. Conformance
tests verify reachable terminal states and progress; build validation cannot
prove termination of arbitrary loop implementations. Unsupported capability,
unknown tool, invalid transition, deadline, limit, policy halt, and cancellation
are typed outcomes. They are not `NotSupportedException`, magic assistant text,
or a successful result with a null output.

## Cancellation, concurrency, and ownership

Each await receives the token owned by that phase. An invocation token cancels
admission before acceptance or that caller's wait after acceptance; it does not
abort accepted work or another joiner's run, including work in an explicitly
ephemeral session. Operation-owned cancellation combines authorized aborts,
deadlines, budget stops, and the host's explicit shutdown policy while
preserving the reason. A separately named owning-run API may bind caller
cancellation to an abort, and must document that ownership. Required terminal
persistence and audit use a bounded independent settlement token. Cancellation
stops new effects and follows drain policy; it never rewrites committed state or
prevents recording why work stopped.

One scoped loop instance executes one run. Different run scopes may execute in
parallel, including runs for different agents that select the same stateless
collaborators. The session run coordinator permits one operation per execution
lane; different lanes may overlap effects. The session coordinator serializes
durable mutations for a `SessionId`. Within a run, the tool scheduler—not the
loop—owns tool concurrency. All run-owned tasks are complete, safely detached to
a documented owner, or durably handed off before the result reports settlement.

## Related concept specifications

- [Agent loop state machine](../concepts/agent-loop-state-machine.md)
- [Run lifecycle and settlement](../concepts/run-lifecycle-and-settlement.md)
- [Usage limits and budgets](../concepts/usage-limits-and-budgets.md)
- [Cancellation, timeouts, and resilience](../concepts/cancellation-timeouts-and-resilience.md)
