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

`ConfigurationLayerKind` names exactly these eight layers. A run captures its
actual configured precedence rather than assuming this default order is
universal. Equal-layer sources have deterministic captured ordering, and every
effective entry retains its complete ordered contributor closure.

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

## Effective configuration snapshot

The result of layering is an immutable `EffectiveConfigurationSnapshot` with a
stable positive version, canonical content fingerprint, complete effective
semantic entries, and complete source-publication provenance. It is not a raw
configuration-provider view, mutable options object, or free-form extension bag.
Every entry identifies its semantic path, declared merge operation, owned value,
and ordered contributors. Profile and component selections retain their typed
key, version, and fingerprint evidence; credentials, endpoint secrets, and
account material remain behind their owning classified profile boundary.

The initial semantic value family contains an owned bounded JSON value and
explicit typed cases for `SecurityProfilePublication`,
`SessionProfileReference`, `ModelSelectionPolicy`, and `ToolsetPublication`.
JSON is available only to settings whose declared schema admits document data;
it is not an escape hatch for a selection. Further selection families add a
typed case and canonical codec support before the compiler can publish them.

Snapshot construction validates local representation and contributor/source
closure. The configuration compiler separately uses every setting's declared
schema and merge metadata to prove complete paths, correct value cases, merge
behavior, and cross-setting constraints. Constructor acceptance alone is not a
claim that the configuration is complete or compilable.

The configuration publication authority coordinates separate replaceable
source-discovery, schema-aware merge, canonical-encoding, fingerprinting, and
retained-storage contracts. A run publication pins the exact snapshot before
admission and passes it through the explicit run context. Turn-boundary changes
publish and capture another complete snapshot. Consumers must not reread
`IConfiguration`, options monitors, current catalogs, environment variables, or
files to reconstruct a captured value.

Durable state retains the snapshot version and fingerprint plus enough source
publication evidence to resolve the same semantic snapshot. Missing retained
content is an unavailable or migration decision, never permission to use the
latest snapshot.

## External configuration sources and trust

`ConfigurationSourceId` and `ConfigurationPath` are nonblank ordinal value
identities. The configuration compiler, not string spelling alone, validates a
path against its declared namespace and setting grammar.
`ConfigurationSourceVersion` is a positive published long revision.
`ConfigurationMergeOperation` is exactly `Replace`, `DeepMerge`, `Append`,
`KeyedMerge`, `RuleList`, or `Reset`; a setting declares one.
`ConfigurationTrustClass` is initially closed to `Untrusted` and
`HostEstablished`.

Configuration discovery MUST define source locations and precedence. External
resource content is untrusted until a host-owned source binding establishes its
trust class. Source content, including its extension data, cannot self-promote
from `Untrusted` to `HostEstablished`. Trust classifies handling only; it grants
no authority and cannot override a managed constraint. Untrusted configuration
MUST NOT load executable extensions, change permission policy, inject
credentials, or widen filesystem/network scope.

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
- A configuration file that declares itself `HostEstablished` is rejected unless
  the host-owned source binding independently supplied that trust class.
- A next-turn override expires exactly after its named boundary.
- Two provider operations bound to different endpoint/account profiles cannot
  exchange options or credentials through registration order.
- A provider payload-extension collision follows its declared merge rule or
  fails before I/O; it never overrides a protected typed field.
- Two snapshots with the same semantic entries and source publications produce
  the same fingerprint regardless of input dictionary order.
- A changed effective value or selected profile reference changes the
  fingerprint even when the publisher version is accidentally reused, and the
  conflicting publication is rejected.
- A run continues to expose the exact captured snapshot after a newer
  publication becomes current; recovery resolves the retained version and
  fingerprint or returns an explicit unavailable or migration outcome.
- Snapshot diagnostics and durable manifests contain profile references and
  source fingerprints but no credential, endpoint secret, or account material.

## Related specifications

- [Context assembly and instructions](context-assembly-and-instructions.md)
- [Extensions, hooks, and middleware](extensions-hooks-and-middleware.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
