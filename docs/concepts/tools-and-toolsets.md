# Tools and toolsets

**Status:** Normative

**Architecture:** [Tools](../architecture/tools.md)

**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[model capabilities](model-providers-and-capabilities.md)

## Purpose

This specification defines discoverable tool identity and schema; the
[tool-call lifecycle](tool-call-lifecycle.md) owns authorization, invocation,
and terminal recording for a model-requested call.

A tool is an application-controlled executable capability that a model may
request. Description, discovery, resolution, validation, authorization,
invocation, and recording are separate responsibilities.

## Identity and descriptor

The canonical public `ToolDescriptor` shape is defined once in the
[tool architecture](../architecture/tools.md#normative-minimal-contract-shape).
This concept owns descriptor behavior, trust, and discovery semantics rather
than a parallel API declaration.

`ToolId` is stable independent of display name. A provider-visible name is an
adapter-safe alias and MUST map back to one exact tool/version. Duplicate names
MUST be rejected or resolved by explicit deterministic precedence before the
request is sent.

An alias returned by a provider remains the requested alias until resolution. An
unknown or ambiguous value receives a rejected terminal result with no
fabricated `ToolId` or `ToolVersion`; textual equality with a canonical ID does
not make it one.

Descriptions, schemas, MCP annotations, and model-selected names are untrusted
metadata. They never grant authority.

## Contract separation

- `IToolProvider` discovers one source publication and returns an owned capture
  that can acquire invokers from that exact publication.
- `IToolCatalog` combines snapshots, reports collisions, and returns an owned
  capture retaining the selected provider captures.
- `IToolResolver` binds a stable identity/version to an invoker lease through
  that catalog capture.
- `IToolArgumentValidator` validates bounded parsed arguments.
- `ISecurityAuthority` evaluates the canonical tool operation and returns allow
  with a bounded grant, deny, or require approval.
- `IToolInvoker` performs one already-authorized invocation and returns raw
  evidence for executor-owned normalization and terminal recording.
- `IToolCallRecorder` commits accepted calls and their authoritative terminal
  results.
- `IToolResultProjectionPolicyCatalog` retains captured policy versions, and
  `IToolResultProjector` creates the bounded loss-aware history/model value
  without invoking the tool.
- Security and tool audit sinks receive correlated redacted decisions, grant
  consumption, and outcomes.

Convenience function/reflection tools MAY implement these through adapters.
Reflection and dynamic binding MUST NOT define the core contract.

## Toolsets

A toolset is a composable provider of tool descriptors and invokers. Toolsets
MAY be static, dynamic per run, filtered, prefixed, remote, or capability-
backed. Combining toolsets MUST preserve source identity and deterministic
ordering.

Toolset preparation occurs before each model request when availability can
change. A preparation stage MAY add, remove, rename, or annotate tools based on
run context and model capabilities, but its result MUST be immutable and
captured in the request manifest.

Discovery carries the complete authenticated `ExecutionIdentity`, immutable
security/definition/configuration snapshots, and typed run correlation. Those
values MUST agree before any dynamic, remote, or MCP provider is contacted.
Principal-specific discovery is authorized, and caches include identity plus
authority, policy, definition, configuration, source, and model-capability
versions; a process-level engine never shares one agent's exposed catalog with
another identity by accident.

The discovery request requires the same agent, session, and active run in its
authorization scope. Before-run or after-run correlation does not become an
active-run scope merely by retaining the same causal run ID. Authored toolset
keys are unique and compared exactly; an empty selection and definition revision
zero are valid. Constructing the request validates local coherence, while the
catalog still resolves publications and preflights capability/schema support.

A provider snapshot retains its explicit source ID, exact source version, and
ordered descriptors. Every descriptor belongs to that source, and an exact
tool/version identity appears at most once in the publication. Repeated display
names across distinct identities are valid discovery data; catalog merge and
alias policy decide exposure before model I/O. The catalog snapshot retains
every acquired source version, including selected empty sources.

Snapshots contain only immutable evidence. Their companion provider and catalog
captures own live acquisitions; a serialized snapshot cannot recover an invoker
by looking up current registrations. Invoker leases retain the exact descriptor
and source version. Closing a capture prevents new acquisitions while existing
leases retain their bindings until released. Closure waits for all leases and
owned cleanup. A caller holding a lease must release it before awaiting closure
on that same control path. Released leases reject invoker access but keep their
immutable metadata readable. Concurrent and repeated disposal share the same
completion and cleanup failure; cleanup is never retried implicitly. Closed
source acquisition returns an unavailable result for the exact requested
identity. Cancellation before transfer acquires no resource.

Failed or cancelled capture releases its partial acquisitions and advertises
nothing. Borrowed host-DI instances keep their original disposal owner; release
never proves an external effect stopped.

## Schema rules

Descriptors retain an owned `JsonSchema` for each input schema and optional
output schema. The value records its exact `JsonSchemaDialectId`; it does not
compile the schema, resolve references, or declare keyword support. Every
published descriptor also retains its exact nondefault tool version and stable
source identity, so a catalog never reconstructs either from registration order
or a CLR type.

Input schemas MUST use a declared JSON Schema dialect and provider translation
profile. The runtime MUST retain a canonical full schema for validation even
when an adapter must downgrade the model-visible schema.

Arguments are bounded before JSON parsing, then validated against the canonical
schema and any typed validator. Schema defaults SHOULD NOT be applied unless the
tool contract declares that behavior. Additional properties and numeric, string,
array, and recursion limits MUST be explicit.

Output schemas describe result data but do not excuse output bounds,
serialization validation, or redaction.

## Effects and execution hints

Optional effect and hint evidence distinguishes an unasserted value from an
explicit claim. Null idempotency or resource-kind evidence does not imply
idempotence or absence of protected effects; an initialized empty resource-kind
collection is the explicit-none representation. `Unspecified` scheduling does
not mean parallel-safe. A concurrency key is present exactly for
`ConcurrencyKey` scheduling, while expected duration and approval-cacheability
remain nullable when the publisher makes no claim.

A mutating effect cannot declare `IdempotencyClassification.ReadOnly`, whose
meaning is that the operation performs no external mutation. Read-only tools may
still omit replay evidence or declare a stricter classification; policy never
widens from a conservative claim.

Descriptors SHOULD declare effect class, idempotency, required resource scope,
expected duration, concurrency key, sequential/barrier requirement, and whether
approval can be cached. These are policy and scheduling inputs, not trusted
claims from remote tool sources.

Host policy MAY override or distrust hints. An unknown effect class fails closed
for authorization.

## Dynamic and provider-native tools

[MCP tools](mcp-integration.md) enter as remote catalog snapshots, while
[provider-native tools](model-providers-and-capabilities.md) retain their
provider-owned execution and billing lifecycle.

Dynamic tools MAY appear or disappear between turns. Calls resolve against the
catalog snapshot used in the originating request, not whatever catalog happens
to exist later.

When toolsets are merged, the snapshot MUST retain a per-tool reference to the
originating execution-policy key and version. Resolution, validation, and
planning preserve it, and the run capability supplies the exact bound policy
instance. Validation produces validated arguments; only a later policy decision
may produce an execution plan.

A tool result MAY propose tools to activate for later requests. Activation
occurs only when that result is durably materialized in source order,
deduplicates names deterministically, resolves them against the captured/next
catalog, and passes capability and authority checks. It never changes the batch
or provider request already in flight.

Provider-native tools remain distinct from application tools because execution,
permission, billing, and result lifecycles differ. Adapters MUST NOT disguise a
server-side web search as a locally authorized function call.

## First-party file tools

First-party file tools are separate feature packages. Observation uses
`AgentKit.Tools.Read`, `.List`, `.Glob`, and `.Search`; mutation uses
`AgentKit.Tools.Write`, `.Edit`, and `.Patch`. Each owns only its descriptor,
model-facing projection, options, and registration and depends on the narrow
file-system abstractions it consumes. None owns host path resolution, byte
enforcement, or operating-system I/O.

A convenience registration MAY install several packages, but a combined
`AgentKit.Tools.FileSystem` package is not the normative ownership boundary.
Read-only agents MUST be able to select observation tools without receiving a
writer or another mutating descriptor.

Read ranges are a model-facing concern. The read tool defines one-based logical
line offsets, a positive finite line limit, EOF and trailing-newline behavior,
truncation markers, and a version-bound continuation; an omitted limit uses a
configured finite window. The host supplies a bounded byte stream and MUST NOT
implement the request by loading an unbounded file before slicing it. The write
tool requires one explicit `CreateOnly`, `ReplaceExisting`, `CreateOrReplace`,
or `Append` disposition. It has no implicit overwrite mode, accepts empty and
whitespace-only text, and exposes parent-directory creation as a separate
declared and authorized effect.

## Acceptance scenarios

- Duplicate provider-visible names fail before model I/O.
- A dynamic call resolves against its original catalog snapshot.
- A refreshed source cannot change an invoker acquired through the original
  capture; selected empty sources retain their publication versions.
- A foreign descriptor source, repeated exact identity, or missing catalog
  source version rejects construction. Display-name collisions are resolved only
  by explicit catalog policy.
- Mismatched discovery identity, session, active run, definition, or
  configuration fails before source I/O, including an after-run correlation with
  the same run ID.
- A failed or cancelled capture releases owned acquisitions once; closing it
  blocks new acquisitions without invalidating an outstanding invoker lease.
- Closing a capture drains outstanding leases before releasing an owned DI
  scope. Borrowed scopes remain host-owned, and cleanup failure is shared by all
  waiters without a repeated cleanup attempt.
- A faster later result cannot activate a tool before its source-order result is
  materialized, and proposed unknown tools remain inactive.
- Canonical validation remains strict after provider schema downgrade.
- Reflection tools pass the same validation and permission pipeline.
- Untrusted remote annotations cannot lower effect class or grant approval.
- Combining toolsets produces stable order and collision diagnostics.
- Selecting only `AgentKit.Tools.Read` does not resolve or authorize a writer.
- Selecting list, glob, or search does not implicitly grant read, process, or
  mutation authority.
- A missing write disposition fails validation instead of defaulting to
  destructive replacement.

## Related specifications

- [Tool-call lifecycle](tool-call-lifecycle.md)
- [Tool errors, retries, and results](tool-errors-retries-and-results.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [MCP integration](mcp-integration.md)
