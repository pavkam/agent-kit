# Tools and toolsets

**Status:** Normative  
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

Descriptions, schemas, MCP annotations, and model-selected names are untrusted
metadata. They never grant authority.

## Contract separation

- `IToolProvider` discovers descriptors from one source.
- `IToolCatalog` combines snapshots and reports collisions.
- `IToolResolver` binds a stable identity/version to an invoker.
- `IToolArgumentValidator` validates bounded parsed arguments.
- `ISecurityAuthority` evaluates the canonical tool operation and returns allow
  with a bounded grant, deny, or require approval.
- `IToolInvoker` performs one already-authorized invocation.
- `IToolResultRecorder` commits terminal results.
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

## Schema rules

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

Provider-native tools remain distinct from application tools because execution,
permission, billing, and result lifecycles differ. Adapters MUST NOT disguise a
server-side web search as a locally authorized function call.

## Acceptance scenarios

- Duplicate provider-visible names fail before model I/O.
- A dynamic call resolves against its original catalog snapshot.
- Canonical validation remains strict after provider schema downgrade.
- Reflection tools pass the same validation and permission pipeline.
- Untrusted remote annotations cannot lower effect class or grant approval.
- Combining toolsets produces stable order and collision diagnostics.

## Upstream evidence

- Pydantic AI's static, combined, dynamic, prepared, prefixed, and external
  toolsets are documented in [toolsets](https://ai.pydantic.dev/toolsets/).
- OpenCode separates application tools from session execution in
  [`application-tools.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/tool/application-tools.ts).
- Pi's core tool definitions and execution options are in
  [`types.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src/types.ts).

## Related specifications

- [Tool-call lifecycle](tool-call-lifecycle.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [MCP integration](mcp-integration.md)
