# Configuration and overrides

**Status:** Normative

**Architecture:**
[Composition and configuration](../architecture/composition-and-configuration.md)

**Depends on:** [Design principles](design-principles.md),
[architecture and dependency boundaries](architecture-and-dependency-boundaries.md),
[model capabilities](model-providers-and-capabilities.md)

## Purpose

Configuration is layered immutable input to a run. Override behavior must be
predictable enough that an operator can explain every effective value.

## Configuration closure and defaults

Every behaviorally meaningful choice MUST be represented at exactly one proper
configuration boundary:

- DI selects or replaces mechanisms and policy implementations;
- typed options configure one implementation process-wide;
- engine configuration selects shared catalogs, named services, and host policy;
- an immutable agent definition selects that agent's models, tools,
  contributors, output, limits, and strategy keys; and
- run or next-turn options carry genuinely dynamic overrides.

A hard-coded value is acceptable only when it is an invariant, not an
undocumented policy choice. Each configurable member MUST document its default,
merge operation, validation boundary, reload behavior, and whether it can be
overridden at narrower scopes.

First-party feature packages MUST provide sensible defaults for safe mechanical
behavior such as ordering, bounded capacities, timeouts, selectors, and
in-memory coordination. Security defaults fail closed. Values that describe the
outside world—credentials, endpoints, deployment/model availability, durable
storage targets, tenant principals, and granted authority—MUST be supplied
explicitly and MUST NOT be guessed.

A concrete provider package MAY offer a named helper for a well-known public
service endpoint, but selecting that helper is explicit host configuration. It
materializes a versioned endpoint profile containing the service surface,
origin, API version, routing/retention policy, and compatibility profile; the
registration never falls back to it merely because no endpoint was supplied.
Provider operation registrations independently bind an endpoint profile, a
credential/account profile, and an operation/model adapter so several accounts
and surfaces can coexist without registration-order coupling.

## Layers

The default precedence, lowest to highest, SHOULD be:

1. library defaults;
2. host/global configuration;
3. trusted organization policy;
4. application or external-resource configuration;
5. agent definition;
6. composed capabilities;
7. run invocation;
8. named next-turn override.

Managed security policy MAY be intentionally non-overridable. Such fields MUST
be modeled as constraints rather than pretending to be an ordinary earlier
layer.

## Merge algebra

Every configurable member MUST declare one operation:

| Operation   | Semantics                                                    |
| ----------- | ------------------------------------------------------------ |
| Replace     | Highest present scalar/value wins.                           |
| Deep merge  | Object members merge recursively.                            |
| Append      | Ordered values concatenate, preserving source order.         |
| Keyed merge | Stable key selects replacement or combination.               |
| Rule list   | Ordered rules concatenate and evaluation defines precedence. |
| Reset       | Explicit null/reset removes inherited value when allowed.    |

Absence means inherit. Explicit null MUST be distinguishable from absence for
resettable settings. Arrays MUST NOT accidentally deep-merge by index.

Recommended defaults are: scalars replace; instruction and capability lists
append; toolsets combine by stable identity; model settings deep-merge by key;
and security policies use their own ordered-rule semantics.

Provider payload extensions do not inherit a universal dictionary merge. Each
key belongs to a provider/profile namespace and declares its allowed layers,
classification, merge operation, and whether reset is supported. Collisions
without a declared rule fail validation. Extension data cannot shadow typed
identity, destination, authentication, security, budget, correlation, or
protocol fields; attempted protected-field overrides are rejected and diagnosed,
not silently ignored.

## Dynamic overrides

Dynamic configuration functions MAY run once per run or once per model request.
They MUST declare frequency, receive the effective lower-precedence snapshot,
and return only their layer's changes. Evaluation order follows layer order,
before the [request context is assembled](context-assembly-and-instructions.md).

A dynamic value MUST NOT mutate the shared agent definition. Failure is a typed
configuration or context-preparation error. Results SHOULD be captured in the
request configuration manifest, with secrets redacted.

## Scoped overrides

Ambient mutable globals are forbidden. Tests and applications MAY use an
explicit async-disposable override scope only when it is carried through the run
invocation context and cannot leak to unrelated concurrent runs.

A next-turn override expires after that request unless explicitly promoted to
[session configuration](sessions-persistence-and-branching.md). Model, thinking,
tool, and system-instruction overrides MUST apply at a turn boundary, never to
an in-flight request.

## External configuration sources and trust

Configuration discovery MUST define source locations and precedence. External
resource content is untrusted until the host establishes its trust class.
Untrusted configuration MUST NOT load executable extensions, change permission
policy, inject credentials, or widen filesystem/network scope.

Parse or validation failure MUST preserve the last known-good immutable snapshot
and report the rejected source. It MUST NOT replace effective configuration with
partial defaults.

## Source updates and reload

AgentKit does not own configuration-file editing. A mutable configuration source
MUST nevertheless publish through atomic replacement or optimistic versioning,
and the full candidate MUST validate before publication. File-backed sources own
their locking, merge, and watcher mechanics as host or integration behavior.

Runs use captured snapshots. Reload affects only named boundaries of current
runs and new runs according to policy.

## Declarative agent specifications

AgentKit MAY support YAML or JSON agent definitions. The schema MUST be
versioned and validated before DI composition. Declarative output schemas and
template variables MUST distinguish prompt-only descriptions from runtime-
validated CLR types; accepting a JSON schema does not create a trustworthy typed
object by magic, darling.

## Acceptance scenarios

- A table-driven suite proves each field's merge operation across all layers.
- Explicit reset differs from omission.
- Concurrent runs using different override scopes do not leak values.
- Invalid hot reload leaves the last good snapshot active.
- Untrusted project config cannot load code or broaden permissions.
- A next-turn override expires exactly after its named boundary.
- Two provider operations bound to different endpoint/account profiles cannot
  exchange options or credentials through registration order.
- A provider payload-extension collision follows its declared merge rule or
  fails before I/O; it never overrides a protected typed field.

## Related specifications

- [Context assembly and instructions](context-assembly-and-instructions.md)
- [Extensions, hooks, and middleware](extensions-hooks-and-middleware.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
