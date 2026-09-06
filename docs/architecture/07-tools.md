# Tools

**Role:** Turn model-proposed operations into bounded, authorized, observable
application work.

The model may request a tool call. It never invokes application code directly.
The tool component separates every stage so discovery, policy, execution, and
recording remain independently replaceable.

## Package model

AgentKit.Tools contains the optional first-party tool runtime: catalog,
resolution, validation, scheduling, invocation, normalization, and result
coordination. The contracts live in AgentKit.Abstractions, so another runtime
can replace it without referencing this package.

Individual tools are feature packages named AgentKit.Tools.ToolName. The first
set includes AgentKit.Tools.Read, AgentKit.Tools.Write, and
AgentKit.Tools.Skill. Each package owns its descriptor, invoker, options,
registration, and tests. AddReadTool, AddWriteTool, and AddSkillTool register
their features through the service collection.

AgentKit.Tools.Skill deliberately contributes two capabilities: the skill tool
and the context contributor that describes available skills to the model. Both
use the same source identity and are registered together. File-aware tools
depend on file-system abstractions and never on AgentKit.FileSystem itself.

## Tool sources and identity

Tool providers expose immutable descriptor and invoker snapshots from sources
such as application registrations, reflected functions, remote services,
capability packages, or MCP servers. A catalog combines those snapshots,
preserves source-qualified identity, and rejects ambiguous provider-visible
names before a request is sent.

Descriptions, schemas, annotations, and effect hints are untrusted metadata.
Host policy may tighten them. A model call resolves against the exact catalog
snapshot included in its originating request, so a later catalog change cannot
redirect execution.

## Call pipeline

Each call passes through resolution, size bounds, parsing, canonical schema
validation, permission evaluation, approval or deferral, durable call recording,
invocation, result normalization, and terminal recording. No invocation begins
until the accepted call has been recorded. Invalid, denied, unknown, or
ambiguous calls produce no side effect.

The invoker receives only the validated arguments, approved resource scope,
identity, deadline, cancellation, attempt data, safe dependencies, and a bounded
progress channel. It does not receive the loop, arbitrary history, credentials,
or the dependency container.

## Scheduling

Provider source order defines result publication order. Calls may execute
concurrently when their declared and host-verified scheduling policy allows it.
Sequential calls form barriers; concurrency keys prevent conflicting overlap;
global exclusivity is scoped explicitly.

The scheduler preflights the whole batch for identities, validation, hard
limits, and side-effect-free permission checks before starting work. Completion
order may differ from publication order, but every accepted call receives
exactly one terminal result.

## Results and retries

Results contain typed bounded content, status, safe errors, usage, retryability,
and side-effect certainty. Oversized results are rejected, summarized,
truncated, or stored behind authorized references according to explicit policy.

Retries require both a retryable failure and safe execution semantics. Mutating
calls need idempotency or proof that the prior attempt did not start. Raw
exceptions and secret-bearing arguments never enter model-visible results.

Provider-native tools remain distinct because their execution, permission,
billing, and result lifecycles differ from application tools.

Installing or registering a tool makes it discoverable; it does not authorize
invocation. Permission remains a separate required policy boundary.

## Related concept specifications

- [Tools and toolsets](../concepts/tools-and-toolsets.md)
- [Tool-call lifecycle](../concepts/tool-call-lifecycle.md)
- [Tool scheduling and concurrency](../concepts/tool-scheduling-and-concurrency.md)
- [Tool errors, retries, and results](../concepts/tool-errors-retries-and-results.md)
