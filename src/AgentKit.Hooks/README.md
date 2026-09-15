# AgentKit.Hooks

Dispatch typed lifecycle hooks with ordering, mutation validation, and failure
policy.

Use hooks to observe or customize the boundaries that explicitly permit it. Hook
registrations do not grant security authority or replace the component that owns
an operation.

## Use this project

Start with `AddAgentHooks` in [ServiceExtensions.cs](ServiceExtensions.cs). Read
the overloads and XML documentation for required collaborators, lifetimes, and
duplicate-registration behavior.

`AddAgentHooks` accepts an optional `Action<AgentHookOptions>` that sets host
ceilings for the first-party dispatcher:

```csharp
services.AddAgentHooks(o => o.MaximumInvocationDepth = 4);
```

The options are validated at startup. Each value composes monotonically with
the per-call arguments of `IHookDispatcher.DispatchAsync`: a caller may tighten
a dispatch but never relax past the host ceiling.

| Member                  | Default   | Effect                                                                                                                                   |
| ----------------------- | --------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `MaximumInvocationDepth` | `8`       | Hard ceiling on reentrant dispatches of one point per call path. Effective limit is `min(callerMaxReentrantDepth, MaximumInvocationDepth)`. |
| `MinimumFailureMode`    | `Isolate` | Least strict `HookFailureMode` permitted (`Isolate < FailOperation`). Effective mode is the stricter of the caller's mode and this value. |

Only these two members are implemented today; the timeout, mutation-dispatch,
and reload settings described in the
[hooks architecture](../../docs/architecture/extensions.md) are added when the
dispatcher honors them.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Abstractions](../AgentKit.Abstractions/README.md) — implement
  AgentKit extensions against provider-neutral contracts and typed domain
  values.
- [AgentKit.Loop](../AgentKit.Loop/README.md) — coordinate turns, context
  preparation, model attempts, tool calls, and terminal outcomes.
- [AgentKit.Observability](../AgentKit.Observability/README.md) — share AgentKit
  logging, activity, metric, and tag conventions across components.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Hooks.Tests](../../tests/AgentKit.Hooks.Tests/README.md) — focused
  behavior and registration tests.
- [Component specification](../../docs/architecture/extensions.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
