# Architecture implementation progress

This is a work ledger, not a specification. The
[architecture](architecture/index.md) and its linked normative concepts and
profiles remain authoritative. A green existing test suite does not establish
conformance to requirements it does not exercise.

## Objective and operating rules

Implement all missing parts of the documented architecture and correct divergent
implementations. Terra and Sol agents perform implementation; the coordinating
architect assigns ownership, reviews decisions and evidence, and commits
verified checkpoints. Preserve concurrent work and record unresolved
requirements rather than weakening specifications to match existing code.

Each component closes only after its contracts, first-party implementations,
registrations, capabilities, failure semantics, diagnostics, reusable
conformance tests, and affected composed scenarios are verified. Linked provider
and coding-harness requirements remain in scope. Illustrative future
alternatives are distinguished from explicitly required implementations by the
owning spec.

## Checkpoints

| Checkpoint                                             | Evidence                                                                                                                                                          | Result                                                                                                                                                |
| ------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| Authoritative architecture                             | `0c73575`; Markdown lint across 127 documentation/guidance files                                                                                                  | Committed                                                                                                                                             |
| Existing implementation baseline                       | `ef6582e`; solution build: zero warnings/errors; 2,821 existing tests run                                                                                         | Committed; 2,820 pass, one session-store activity-correlation failure; architecture conformance unproven                                              |
| Budget accounting and batch admission                  | Focused budgets: 67 passed; abstractions: 1,403 passed; atomic hierarchy, started/unknown preservation, batch replay and revisioned correction                    | Verified checkpoint; ledger/profile and consumer integration remain open                                                                              |
| Policy contribution validation and grant evidence      | Focused permissions suite: 23 passed; malformed policy contributions deny, cancellation propagates, replay/concurrency tested                                     | Verified checkpoint; broader permissions work remains open                                                                                            |
| Observation-test isolation and shared test support     | Focused Session.InMemory suite: 49 passed; source/correlation isolation, duplicate tags and helper validation                                                     | Verified checkpoint; shared support established                                                                                                       |
| Reusable grant-store conformance and solution coverage | Focused permissions suite: 31 passed, including eight inherited contract cases; all 120 current projects registered in the solution                               | Verified checkpoint; other contract suites remain open                                                                                                |
| Session tenant isolation and replay evidence           | Focused Session.InMemory suite: 61 passed; foreign-tenant masking, structural replay, changed-request conflicts, original branch receipts and deletion tombstones | Verified checkpoint; identity authentication, grants, durable retention and lane coordination remain open                                             |
| Pinned-agent admission and retained-value integrity    | Focused facade suite: 59 passed; replacement/removal, reconstructed values, scope failure, schema ownership, one terminal admission and isolated diagnostics      | Verified checkpoint; keyed run-plan composition, retention/revocation catalogs and waiter cancellation remain open                                    |
| Trusted execution identity and delegation              | Focused identity runtime: 58 passed; rich contracts and migration included in full 3,440-test integration pass                                                    | Verified checkpoint; downstream admission/revalidation and reusable identity conformance remain open                                                  |
| Catalog bootstrap and publication                      | Focused facade/catalog: 91 passed; abstractions: 1,448 passed; full integration: 3,440 passed                                                                     | Verified checkpoint; no-I/O readiness, immutable revision bindings and atomic publication; complete selected graph validation remains open            |
| Selected capability requirements                       | `5148df1`; focused contract tests: 13 passed                                                                                                                      | Removed best-effort resolution of a selected capability; omission is the optionality boundary                                                         |
| Reproducible artifact contract capture                 | Clean archive exposed an ignored source directory; Abstractions build: zero warnings/errors; focused artifact contracts: 10 passed                                | Anchored build-output ignore rule and captured 38 existing contract files plus their tests; artifact conformance remains open                         |
| Typed profile selection identities                     | `438f974`; 31 focused key tests                                                                                                                                   | Six missing profile keys added; complete selected graph remains open                                                                                  |
| Public API baseline                                    | `9d2c6ed`; 60 snapshots, 63 focused checks, 3,415 clean solution tests                                                                                            | Reproducible baseline; behavioral compatibility remains a separate concern                                                                            |
| Identity narrowing conformance                         | `f053ff3`; full Identity suite: 73 passed, including 15 reusable cases                                                                                            | Prevents subject replacement and successive claim/assurance widening; downstream admission remains open                                               |
| Artifact grant evidence                                | `50293ac`; 15 helper checks, 9 coordinator tests, 12 store tests                                                                                                  | Versioned complete-reference binding and denial after reference mutation                                                                              |
| Project graph enforcement                              | `b903457`; 15 checks in Debug and 15 in Release                                                                                                                   | DAG and core inward edges enforced; constructor graphs and leaf-to-leaf ownership remain open                                                         |
| Artifact tombstones and publication                    | `c8131ad`; full InMemory artifact suite: 18 passed                                                                                                                | Exact tenant/reference replay, immutable-version collision rejection and atomic publication winner                                                    |
| Authorization evidence and scope validation            | `33467ed`, `0c91ed8`; clean integrated solution: 3,503 passed; compatibility: 63 passed                                                                           | Six additive evidence types; constructor and record-copy validation; capture/selector runtime remains open                                            |
| Artifact tenant partitions                             | `9a59a07`; full InMemory artifact suite: 29 passed                                                                                                                | Tenant-qualified lifecycle state, unknown-abort semantics, direct key validation and independent read/delete behavior                                 |
| Provider HTTP failures and cancellation                | `c765ee8`; isolated Anthropic: 80 passed; Gemini: 91 passed                                                                                                       | Stable failure categories, bounded safe messages, retained transport diagnostics, one terminal notification and caller cancellation                   |
| Complete artifact preparation binding                  | `1527997`; artifact contracts: 33 passed; coordinator: 12 passed; InMemory store: 39 passed; compatibility: 63 passed                                             | Complete staging authority, immutable options capture and stable replay without extending the original receipt lifetime                               |
| Security publication and activation ownership          | `260bf5b`; architecture Markdown checks passed                                                                                                                    | Exact retained selections, fixed implementation bindings within a composition and operation-owned activation leases specified                         |
| Output diagnostic bounds                               | `4737dcd`; Output: 58 passed; Abstractions: 1,573 passed                                                                                                          | Uniform effective limit in failure records and repair text; invalid policy copies rejected                                                            |
| Reusable artifact-store conformance                    | `862f56d`; InMemory store: 39 passed, including eight shared cases                                                                                                | Lifecycle, replay, authority and tenant assertions preserved across adapter fixtures                                                                  |
| Output text bounds before parsing                      | `de4910b`; Output: 64 passed                                                                                                                                      | Exact UTF-8 bounds before aggregation/parse, including split surrogate pairs                                                                          |
| Owned schema values                                    | `ad7ce18`, `c4c4253`; 14 focused schema cases; clean integrated solution: 3,577 passed                                                                            | Detached DOM ownership, copy validation, structural equality and reviewed API baseline                                                                |
| Independent filesystem capabilities                    | `c566dc5`; isolated filesystem suite: 82 passed                                                                                                                   | Replacing the facade or any narrow contract preserves remaining defaults and shared ownership                                                         |
| Keyed schema preflight and execution contracts         | `e718eee`, `11e80f9`; isolated Release build: zero warnings/errors; full solution: 3,714 passed, no skips                                                         | Versioned schema-engine evidence, keyed profile isolation, bounded preflight and after-run budget capability values; runtime integration remains open |
| Retained output-definition integrity                   | `a054e1d`; 32 focused constructor/copy cases; full compatibility suite passes                                                                                     | Default identities, invalid names/enums, malformed collections and null policies reject before assignment; valid copies preserve their source         |
| Materialized JSON allocation bounds                    | `211b0f8`; Output: 144 passed; old processor fails all three million-token allocation regressions                                                                 | Bounded traversal, token serialization and canonical-byte deserialization; duplicate-name allocation follows aggregate byte validation                |
| Repository analyzer and fixture formatting             | `48c4bc5`; full format/lint/build/test gates pass in the matching isolated checkout                                                                               | Import order, redundant namespace qualification, JSON annotations and fixture whitespace corrected without replacing concurrent edits                 |
| Provider embedding API compatibility                   | `0c1a36a`, `e3ffa47`; original eight-parameter constructor retained; nine reviewed additive provider snapshots                                                    | Binary constructor compatibility restored; full provider wire conformance remains open                                                                |
| Declared component graph validation                    | `bd1101c`; exact-key cardinality, cycles, factory ownership/disposal, captive scopes and a 10,000-node chain exercised                                            | Pure graph validation verified; registration capture and runnable-graph activation remain open                                                        |
| Continuation and input-promotion evidence              | `bd1101c`; Abstractions: 1,867 passed; Loop: 51 passed; IO: 29 passed; reusable policy cases included                                                             | Safe-boundary proposals, complete ordered evidence, cutoff checks, typed outcomes and constructor constraints verified                                |
| Diagnostic failure isolation                           | `bd1101c`; Observability: 17 passed; policy suites cover throwing listeners/loggers/meters, cancellation and failed clock measurements                            | Policy outcomes survive observer failures; unavailable timing emits no fabricated zero duration                                                       |
| GUID execution-lane identity                           | Isolated Release solution: 4,193 passed; full lint passed; GUID identity conformance and exact empty-value rejection                                              | Intentional constructor/property API correction to the normative session identity; lane runtime integration remains open                              |
| Exact authority selection                              | Isolated Release solution: 4,214 passed; Permissions: 52 passed; full format/lint passed; three additive API snapshots reviewed                                   | Explicit bindings, typed missing-key results and isolated diagnostics verified; policy capture, audit and session integration remain open             |

## Latest integration evidence

The optional-dependency cardinality checkpoint passed complete formatting, lint,
Release build, and test gates in an isolated checkout over `d985f6e`. The
reviewed API change adds `OptionalSingular = 2` while preserving existing enum
values.

Declared optional dependencies permit zero or one exact matching registration.
Multiple matches reject as ambiguous; a present match participates in complete
cycle and singleton-captivity validation. An optional dependency cannot claim an
operation-owned factory boundary, whose root still requires singular presence.
Focused tests cover absence, presence, ambiguity, cycles, captive scopes, and
invalid factory metadata. This extends declared graph semantics only; metadata
for actual optional Microsoft DI infrastructure, open-generic correspondence,
first-party descriptor registration, and complete runnable activation remain
open.

The durability-carrier checkpoint passed complete formatting, lint, Release
build, and test gates in an isolated checkout over `2cbfab5`. Its one additive
API snapshot exposes the canonical type-qualified guard for complete durable
operation results.

`DurableOperationResult` accepts `OutcomeReady`, `Completed`, and `Faulted` in
construction and record copies. `Accepted`, `EffectPending`, `Waiting`, and
undefined states reject before assignment. `OutcomeReady` retains the complete
semantic result while publication may remain pending; recording it never
licenses another effect invocation. Certainty stays independent and truthful,
including unknown effects after failure or cancellation.

`DurableExecutionContext` rejects default profile, backend, journal,
lease-manager, and recovery-policy string keys in construction and copies.
Durability profile revision zero remains valid under its owning contract. Tests
cover exact exception types and parameter names, all permitted result states,
failed copies, and preservation of the original value. Complete captured
authorization, address/correlation coherence, runtime activation, journal
implementation, and required-audit settlement remain open.

The provider-bound component-registration checkpoint passed complete formatting,
lint, Release build, and test gates in an isolated checkout over `aa81185`.
Three additive API snapshots were reviewed. Component declarations now match
exact Microsoft DI contract addresses, ordinal keys, lifetimes, and observable
implementation types. Opaque factories require explicit metadata; validation
never invokes them to discover dependencies. Duplicate, missing, mismatched, and
opaque metadata registrations are rejected before application service effects.

`AgentKitServiceProviderFactory` captures the completed service collection once
per provider build, validates the declared graph, and retains that exact
immutable evidence with the engine. Later collection mutation cannot replace
provider A's evidence; provider B validates its own collection. Captured
provider options and normal Microsoft DI ownership/disposal are preserved. Null
registrations reject before diagnostics or factory effects.

Hosted callers must select `AgentKitServiceProviderFactory` through the standard
.NET host factory boundary, or use it directly for a custom service collection.
Resolving an engine from a plain `BuildServiceProvider()` now fails closed with
`agentkit.component-registration.snapshot-missing`. This is an intentional
behavioral migration. Standalone `AgentEngineBuilder.Build()` uses the same
factory automatically. The additive `DeclareAgentKitComponent` extension
publishes metadata only; it does not register an implementation or build a
provider.

Provider construction emits safe shared activities, bounded count/duration
metrics, and source-generated bootstrap logs. Its explicitly supplied clock and
logger are caller-owned. Failed or decreasing timing omits duration without
changing construction; instrument publication holds no lock across listener
callbacks. Tests exercise reentry, concurrent publication, exact clock values,
observer failure, and trace-scoped parallel observation.

This remains partial graph evidence: the snapshot explicitly does not attest to
the complete runnable graph. First-party descriptor registration, full keyed
component/profile selection, run-plan compilation, and runtime activation remain
open.

The broker observability checkpoint passed `make format`, `make lint`, and
`make test` in an isolated checkout over `05df67a`. All 4,741 tests passed
without skips; the Release build reported zero warnings or errors. Three
additive public API snapshots were reviewed. Owning IO and Goals suites contain
51 and 30 passing tests; downstream Question and Task suites contain 17 and 12.

Human-question publication and task delegation now emit shared activities,
source-generated structured logs, and outcome-only count/duration metrics. Trace
and log identities retain correlation without capturing prompts, answers, task
objectives, child results, or exception messages. Clocks and loggers are
replaceable, and existing broker constructors remain available. Observer
failures preserve semantic results and exceptions; cancellation is checked after
a non-cooperative channel returns. Caller-wait cancellation does not assert that
a displayed question was revoked or child work stopped.

A channel result with a different question/delegation identity remains the
returned object, while its observation records failure at error severity. Tests
scope throwing listeners and metric collectors to their own parent trace and use
concurrent-safe collectors. Direct metric and enum guards reject invalid values
before measurement. Shared session instrumentation from the preceding checkpoint
is retained. This checkpoint adds routine operational telemetry; required audit
delivery and durable post-effect settlement remain open.

The preceding session/facade checkpoint passed `make format`, `make lint`, and
`make test` in an isolated checkout over `bb4a2d4`. All 4,710 tests passed
without skips; the Release build reported zero warnings or errors. The initial
complete run passed 4,682 tests and failed only seven expected API snapshots,
all reviewed. The final run also includes descriptor-construction and
copy-invariant regressions, accepted-state coherence checks, and scoped
diagnostic listeners that cannot interfere with parallel receipt tests. Source
integration used verified frozen bundles; earlier language, artifact, and broker
checkpoint bytes were retained.

Session operations now carry immutable captured authorization and an explicit
session profile. Creation resolves an authorized sessionless directory route
before allocating the addressed store operation. The directory and store require
fresh exact enforcement receipts and accepted pre-access audit; a consumed grant
is never treated as atomic with an arbitrary external effect. Store routing
captures descriptors once, preserves exact keys, and rejects incompatible
capabilities without fallback. Constructor guards precede enumeration, and
invalid record copies cannot bypass descriptor validation.

The in-memory store adds explicit lane provisioning, idempotent input admission,
and atomic promotion plus accepted-run state. Whole-session versions, lane
revisions, branch cursors, identity, and reserved entry/message IDs participate
in the transaction checks. The start request must match the initiating
admission’s before-run correlation and bind consistent authorization and
configuration evidence. The trigger identity survives promotion and
accepted-state loading. Recovery values reject reused history identities,
unchanged committed tips, and self-parenting entries. Accepted state can be
loaded with its retained evidence. This process-local implementation does not
establish process-loss recovery, distributed fencing, or complete run
settlement. Reusable store conformance and real captured-grant integration
scenarios are included.

Facade readiness captures one exact profile-publication snapshot and revalidates
it before admission. The validated snapshot is reused without a second read;
cancellation after publication/capture prevents dependent work. Loop, tool,
compaction, and Plan callers carry the captured evidence. Plan receipt
validation accepts reconstructed value-equal resources and rejects changed
authority or resources. Post-commit observational failures preserve committed
session results. Full keyed runnable composition, the remaining lifecycle
transitions, durable recovery, and complete required-audit settlement remain
open.

This checkpoint intentionally changes caller and adapter contracts to match the
[session](architecture/sessions.md) and
[composition](architecture/composition-and-configuration.md) architecture:

| Affected surface                                                        | Required migration                                                                                                                                                                                   |
| ----------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Runnable `AgentDefinition`                                              | Select security and session profile keys and publish the exact agent/revision binding. The legacy constructor remains available for catalog-only values but no longer yields a runnable definition.  |
| `AgentRunRequest`, `ToolExecutionContext`, `CompactionOperationContext` | Supply captured authorization and the compiled session profile; only sessionless tool contexts may omit that profile. Reconstruct protected causal coordinates instead of changing them with `with`. |
| `SessionOperationContext`, `SessionCreateRequest`                       | Supply exact captured authority and an optional lane where appropriate. Creation requires sessionless BeforeRun evidence, followed by a fresh addressed context.                                     |
| `ISessionCoordinator`                                                   | Pass the selected immutable profile to every operation; update custom implementations to the new signatures.                                                                                         |
| `ISessionStore`                                                         | Accept authorized wrappers, advertise an audience, enforce fresh receipts and required audit, and implement lane, admission, run-acceptance, and accepted-state operations.                          |
| `SessionStoreDescriptor`                                                | Declare real capabilities, consistency, durability, and fencing support through the replacement constructor. Constructor and init assignments both validate their values.                            |
| Session append/read/branch/delete requests                              | Use validating constructors instead of init mutation; preserve whole-session CAS versions independently from branch cursors.                                                                         |
| Plan request records                                                    | Supply the exact session profile and reconstruct requests for new mutations. The existing Plan store constructor remains available.                                                                  |
| `SessionBusyBehavior`                                                   | Use `AgentKit.SessionBusyBehavior` from Abstractions; the former Session-package type is removed. `Reject` and `Wait` retain their meanings.                                                         |
| `AddSessionStore<TStore>`                                               | Register each intended store once with a unique key. Registrations are additive; duplicate effective keys fail validation.                                                                           |
| `DefaultAgentLoop` construction                                         | Supply the selected `ISecurityProfileSelector` required for fresh operation-scoped captures.                                                                                                         |
| `InMemorySessionStore` construction                                     | Supply the audit ID source, audit dispatcher, grant store, branch ID source, and clock. Session ID allocation now belongs to coordinated creation.                                                   |

The preceding human-question and task-delegation receipt checkpoint passed
`make format`, `make lint`, and `make test` in an isolated checkout over
`e927f0a`. All 4,496 tests passed without skips; the Release build reported zero
warnings or errors. The initial run failed only two expected additive
constructor snapshots, both reviewed before final verification. Review also
added four captured-context mismatch cases before the final run.

Both brokers now retain captured authorization, generate a fresh typed intent,
and require an exact freshly consumed receipt before invoking their application
channel. Reconciled, missing, or mismatched receipts cannot publish a question
or dispatch child work. A captured scope or identity mismatch returns a typed
rejection before intent generation or grant consumption; cancellation is checked
immediately after consumption. Existing constructors remain available and intent
generation is replaceable through DI. The owning IO and Goals suites pass 37 and
13 tests respectively, including real captured grants and denial without channel
calls. Required audit delivery, operation instrumentation, and broader IO and
goals architecture remain open.

The preceding scripted-language enforcement checkpoint passed `make format`,
`make lint`, and `make test` in an isolated checkout over `520a9fa`. All 4,480
tests passed without skips; the Release build reported zero warnings or errors.
The initial run failed only the expected additive constructor snapshot, reviewed
before the final run.

Scripted language queries now preserve captured authorization and require a
fresh exact enforcement-intent receipt before delay or result delivery.
Reconciled, missing, or mismatched receipts deny the query, and caller
cancellation is checked immediately after grant consumption. Intent identity
generation is replaceable through DI; both existing constructors remain
available. The 15-test leaf suite includes a real registered captured grant,
receipt rejection, cancellation, generator replacement, and null-argument
checks. The downstream language-tool suite passes all 18 tests with explicit
test-only Abstractions and Options references. Required audit and broader
language-service architecture requirements remain open.

The preceding artifact-enforcement checkpoint passed `make format`, `make lint`,
and `make test` in an isolated checkout over `87542cf`. All 4,473 tests passed
without skips; the Release build reported zero warnings or errors. The initial
run passed all 4,472 unchanged-API and behavioral tests and failed only the
expected additive constructor snapshot, which was reviewed before final testing.

The in-memory artifact store now requires a fresh exact enforcement receipt
before prepare, finalize, abort, read, or delete accesses state or content.
Captured authorization is retained, and a scope or identity mismatch returns a
typed denial before grant consumption. Reconciled, missing, or mismatched
receipts cannot authorize a new operation; cancellation is checked immediately
after consumption. Intent generation is replaceable through DI, while the
original constructor remains available. The 48-test artifact suite includes
actual captured grants, denial without state changes, cancellation, generator
replacement, and constructor validation. Artifact persistence, lifecycle, audit,
and broader architecture conformance remain separate open requirements.

The preceding filesystem-enforcement checkpoint passed `make format`,
`make lint`, and `make test` in an isolated checkout containing the verified
network and process checkpoints. All 4,464 tests passed without skips; the
Release build reported zero warnings or errors. The initial run exposed one
expected API comparison and two read/write end-to-end fixtures that still used
legacy grant consumption. The fixture receipts and additive constructor snapshot
were reviewed before the final run, which includes direct validation of the new
intent-generator argument.

All eight existing sandboxed filesystem enforcement sites now generate a fresh
typed intent, preserve captured authorization, require an exact freshly consumed
receipt, and recheck cancellation before effects. Reconciled, unsupported,
missing, or mismatched receipts cannot start filesystem work. Read and write
integration uses real captured in-memory grants. Existing write dispositions,
parent-directory behavior, patch checks, and public constructor compatibility
remain covered. Receipt retention is atomic; persistence depends on the selected
grant store. Required audit coverage and the remaining filesystem architecture
requirements stay open.

The preceding process-enforcement checkpoint passed `make format`, `make lint`,
and `make test` in an isolated checkout containing the verified network
checkpoint. All 4,455 tests passed without skips; the Release build reported
zero warnings or errors. The initial run over `2e4e164` passed all 4,426
behavioral and unchanged-API tests, with only two expected API snapshot
failures. Those additive constructor changes were reviewed before final
integration. Direct null-intent-generator checks cover both new process
constructor overloads.

The operating-system and scripted process runners now require a freshly consumed
grant with an exact enforcement-intent receipt before starting a process. They
preserve captured authorization, reject reconciled, unsupported, missing, or
mismatched receipts, and recheck cancellation after consumption. Intent identity
generation is replaceable through DI, while existing public constructors and
explicit null logger arguments remain compatible. Tests use actual captured
grants from the in-memory grant store through both runners. Broader process
isolation, termination, audit, and durable recovery requirements remain open.

The preceding network-enforcement checkpoint passed `make format`, `make lint`,
and `make test` in an isolated checkout over `2e4e164`. All 4,439 tests passed
without skips; the Release build reported zero warnings or errors. The initial
run exposed six composed web-fixture failures and two expected API comparisons.
The strict web fixture now implements exact intent receipts and preserves the
existing two-phase authorization and redirect assertions. Both additive API
snapshots were reviewed before the final run, which also includes direct null
intent-generator validation for all four new constructor overloads.

A later run exposed a race in the unchanged budget-metrics test: its global
listener collected parallel tests' events into an unsynchronized list. The
fixture now selects its two instruments and dedicated bounded dimension, and
retains measurements in a concurrent queue. Ten focused budget-suite runs with
sixteen test threads passed before final solution verification.

The default and scripted DNS resolvers and network transports now require a
fresh `Consumed` result with a receipt matching the intent, grant, request,
fence, fingerprint, captured authorization, and every concrete enforcement
field. Reconciled, unsupported, missing, or mismatched receipts cannot begin a
new effect. Caller cancellation is checked after a noncooperative grant store
returns. Each boundary accepts a replaceable typed intent identifier generator;
existing constructors, including explicit null loggers, remain compatible.
Actual captured grants from `InMemorySecurityGrantStore` succeed through all
four paths. Required audit coverage and the remaining network architecture
requirements stay open.

The preceding grant-enforcement checkpoint (`2e4e164`) passed `make format`,
`make lint`, and `make test` in an isolated checkout over `4e1adf7`. All 4,414
tests passed without skips; the Release build reported zero warnings or errors.
The initial full run passed 4,398 of 4,401 tests and failed only the three
expected API snapshot comparisons. Thirteen direct guard cases were added before
the final run, alongside the reviewed snapshots and documentation correction.

Grant stores can now consume one use and retain its exact enforcement-intent
receipt atomically. An exact retry returns `Reconciled` historical evidence; it
cannot authorize another effect, even when the original grant later expires or
is revoked. Changed effect or fence evidence fails without another spend. The
default interface implementation rejects unsupported intent persistence before
invoking legacy consumption. The in-memory implementation remains process-local
and makes no durability claim.

Captured security requests, grants, and enforcement evidence preserve the exact
authorization context. Constructor and record-copy guards reject scope,
identity, or captured grant policy-version mismatches. The authority captures
its configured policy snapshot and bounds once, so later options mutation cannot
replace a pinned selection. Canonical security fingerprints preserve exact
UTF-16 code units; distinct unpaired surrogates no longer collapse through
replacement encoding. Safe grant-consumption signals cover success,
reconciliation, cancellation, faults, and throwing diagnostic listeners.

The API review preserves the existing constructors and adds intent-aware
contracts and overloads. It intentionally removes the `Status`, `RemainingUses`,
and `SafeMessage` init setters from `GrantConsumptionResult`. This is a source
and binary compatibility change: callers construct a validated result instead of
changing fields independently of its retained receipt. Captured scope and
identity copies also enforce their documented invariants.

Protected session routing, facade profile activation, and process and filesystem
receipt enforcement are still being integrated. Required audit coverage, durable
security-control persistence, live policy retirement, and composed recovery
remain open; this checkpoint does not complete the security subsystem.

The preceding bounded audit-delivery checkpoint (`4e1adf7`) passed
`make format`, `make lint`, and `make test` in an isolated checkout over
`b59831d`. All 4,345 tests passed without skips; the Release build reported zero
warnings or errors. Two reviewed API snapshots add `AuditDeliveryTimeout` and
the typed `SecurityAuditTimedOut` result. The initial full run failed only these
two expected comparisons.

The dispatcher captures a finite per-sink deadline, defaulting to thirty
seconds, and validates the supported timer range before activation. Injected
`TimeProvider` timers cancel cooperative sinks and bound waits on asynchronous
sinks that ignore cancellation. Required delivery returns a timeout result with
unknown durable acceptance; a later successful durable sink may satisfy the
policy after an optional timeout. Caller cancellation retains its original
token. Late sink completion or failure cannot produce another terminal dispatch
outcome.

Ten reusable audit-dispatcher conformance cases now run against the public
Permissions composition. Package tests retain exact terminal-outcome counts,
including after a late sink fault, and cover timer boundaries and safe
diagnostics. The result hierarchy documentation describes typed outcomes
accurately; generated record copy constructors do not enforce a closed
hierarchy.

Required audit coverage across authority, approval, grant lifecycle, session,
and host effects remains incomplete. Durable sink adapters, security-control
persistence bootstrap, retained activation, and composed recovery/settlement
coverage also remain open. These checkpoints establish dispatch and profile
capture, not completion of the permissions subsystem.

The preceding exact security-profile capture checkpoint (`b59831d`) passed
`make format`, `make lint`, and `make test` in an isolated checkout over
`8af9a62`. All 4,324 tests passed without skips; the Release build reported zero
warnings or errors. Three reviewed API snapshots add publication reading,
authorization capture, explicit publication registration, typed results, and
shared diagnostic names.

The default reader freezes publications by agent identity, definition revision,
configuration revision, and profile key. The selector checks every returned
coordinate before binding a fresh immutable context to the supplied operation
scope and authenticated identity. Missing or mismatched publications return
unavailable without selecting a newer profile. Capture grants no authority.
Coverage includes duplicate registration, argument validation, replacement,
cancellation, safe diagnostics, and hostile logging, meter, and activity
listeners. The initial full run failed only the three expected API snapshots.

Host-supplied publications still need integration with validated agent component
selections and the run-plan compiler. Policy activation and retirement,
authorization capture at every protected operation, complete grant binding, and
routed session coordination remain open. C# record copy constructors permit
external derivation despite restricted normal construction; selectors reject
unknown result variants rather than relying on a closed hierarchy.

The preceding security audit-dispatch checkpoint (`8af9a62`) passed
`make format`, `make lint`, and `make test` in an isolated checkout over
`a3fd03b`. All 4,275 tests passed without skips; the Release build reported zero
warnings or errors. The three reviewed API snapshots are additive: typed audit
records, dispatch and sink contracts, safe audit values, explicit sink
registration, delivery policy, and shared diagnostic names. The initial test run
failed only those three expected snapshot comparisons; no behavioral test
failed.

The replaceable dispatcher captures additive host-owned sinks. Required delivery
fails closed unless a suitable sink successfully accepts the record durably;
optional sink failures remain isolated. Cancellation propagates before and after
sink calls, and diagnostics retain truthful dispatch outcomes without exporting
audit fields. Fingerprint fields contain a recomputed digest instead of trusting
caller-supplied text to be safe. Focused coverage includes argument validation,
DI replacement, failure and cancellation, and hostile diagnostic listeners.
Cancellation regressions cover sinks that cancel the caller and then throw a
non-cancellation exception, including required and optional delivery. An
existing compaction observation test now uses the shared concurrent collector,
filtered to its operation, after a full run exposed lost callbacks in its plain
list. Production compaction behavior is unchanged.

This establishes audit dispatch, not complete security audit coverage. Bounded
delivery deadlines, durable sink adapters, authority/approval/session audit
integration, the control-plane persistence graph, grant-consumption intent
receipts, and retained profile capture remain open. The `EnforcementProposed`
event is distinct from proof that a grant was consumed or an effect completed.

The preceding authority-selector checkpoint passed `make format`, `make lint`,
and `make test` in an isolated checkout over `e845419`. All 4,214 tests passed
without skips; the Release build reported zero warnings or errors. The 52
Permissions tests include exact binding, duplicate and argument rejection,
cancellation, safe diagnostics, hostile listeners, and failed timing. The three
reviewed API snapshots add the selector/result contracts, explicit authority
registration, and shared diagnostic names without removing existing API.

The selector captures explicit host-owned singleton bindings and resolves only
the key in the supplied authorization context. Missing bindings return a typed
unavailable result. Existing unkeyed authority registration cannot satisfy that
selection. Security profile publication and capture, policy activation, required
audit, grant-consumption intent receipts, and protected session integration
remain open.

The preceding execution-lane identity correction passed `make test` and
`make lint` in an isolated checkout containing only this checkpoint over
`b78e0e6`. The Release build reported zero warnings or errors; all 4,193 tests
passed with no skips. The public API snapshot records the intentional breaking
change from `ExecutionLaneId(string)` and a string `Value` to a validated GUID
constructor and property, as required by the session architecture. Callers must
supply nonempty GUIDs and keep display names separate. Existing fixtures now use
explicit deterministic GUIDs; identity conformance selects the GUID cases.

The session migration is still in progress. Existing store calls need mandatory
authorized wrappers, while the coordinator must authorize a directory effect
before separately authorizing the selected store effect. Protected routing,
required audit, grant-consumption intent receipts, and atomic accepted operation
state remain outside these verified checkpoints.

An earlier isolated checkout matching committed `bd1101c` passed `make format`,
`make lint`, and `make test` (including the Release build), with zero build
warnings or errors. All 4,194 tests passed with no skips. All 2,690 committed
files were compared byte-for-byte with the verified checkout. The final
five-case diagnostic-outcome mapping test was added after formatting and passed
the subsequent full lint and test gates. Twenty-one reviewed formatter changes
were copied back only after confirming the shared files had not changed.

This baseline includes the embedding work committed in `dbf22d6`. The provider
API review found and repaired the removed eight-parameter
`OpenAICompatibilityProfile` constructor in `0c1a36a`; `e3ffa47` records nine
additive provider snapshots. The foundation snapshot also corrects a stale
nullable annotation on the unnamed `ArgumentException` extension receiver. Its
source declaration was already non-nullable, and a focused extractor regression
now covers that shape. Snapshot approval establishes emitted API compatibility,
not complete provider behavior.

The new policies produce immutable proposals. They do not admit input, install
session ownership, invoke models, or commit continuation transitions. The graph
validator is not yet connected to registration capture or the facade's build
validation. The active reduced loop still lacks output processing and complete
keyed collaborator activation. Those integration requirements remain open.

The next session slice is atomic idle-lane run acceptance. Admission and run
acceptance are separate durable boundaries: authorized accepted-input replay
must reconcile before preprocessing; a later run-start transaction must consume
the selected pending inputs, materialize the initial prompt, advance the branch
tip, write complete initial operation state, and install lane ownership
together. Caller cancellation after that commit detaches the waiter without
aborting accepted work.

An earlier isolated checkout matching committed `48c4bc5` passed `make format`,
`make lint`, and `make test` (including `make build`) in Release. The build
reported zero warnings or errors; all 3,769 tests passed with no skips. This
includes 1,689 abstraction tests, 144 output tests, and the public API
compatibility suite. All 2,515 committed files were compared byte-for-byte with
the verified checkout after integration. Separately owned, uncommitted embedding
work is excluded from this checkpoint.

The three allocation regressions were also run against the original processor in
the isolated checkout. With an 8-byte candidate limit, million-character string
and number tokens each allocated 1,001,136 bytes; a property name allocated
1,001,672 bytes. All three fail the regression's 8,192-byte allocation ceiling
before the fix and pass afterward. A retained-whitespace regression verifies
bounded runtime conversion as well. Schema fingerprint fixtures retain the
existing encoding and numeric-spelling semantics. Constructor and record copy
checks intentionally tighten rejection of invalid values without changing the
emitted public API surface.

An earlier isolated archive matching committed `11e80f9` built the complete
solution in Release with zero warnings or errors and passed all 3,714 tests,
with no failures or skips. This includes the schema-engine contract and keyed
output preflight checkpoint. The archive excludes separately owned, uncommitted
embedding changes. Passing this baseline does not close the remaining output
mode, resource-bound, composition, or budget-integration requirements.

An earlier isolated checkout matching committed `c4c4253` built all 124 solution
projects with zero warnings or errors and passed all 3,577 tests, with no
failures or skips. This includes the 60-package public API baseline, 63
compatibility checks, 15 package-graph checks, 73 identity tests, 39 InMemory
artifact-store tests and 64 output-processor tests. Eight artifact cases now run
through the reusable store conformance suite. The graph suite previously passed
its 15 checks in Release.

The build used `ad7ce18`; its first test run found only the two authored
schema-equality methods missing from the Abstractions snapshot. The final run
included the exact approved two-line test-data update in `c4c4253`. Explicitly
authored members can change the generated baseline even when corresponding
compiler-generated methods already existed; surface inspection does not replace
the compatibility check.

The later filesystem DI checkpoint `c566dc5` independently passed its complete
82-test suite in an isolated checkout with zero build warnings or errors. It is
not included in the 3,577-test integration count above. All reviewed checkpoint
overlays were compared byte-for-byte with the shared checkout before commit. The
earlier clean `1527997` integration passed 3,547 tests.

The earlier `af1c26d` integration passed 3,503 tests. That verification caught
two test assertions formatted only in the earlier isolated authorization run;
`0c91ed8` copies those tested corrections into the repository. It also
identified the deliberate artifact test-assembly access as an API metadata
change; `af1c26d` records that reviewed one-line snapshot update. The final full
test run used that exact committed baseline. Future isolated overlays must
compare every owned source and test file after formatting or repairs, not only
the generated snapshots.

The earlier isolated `c8131ad` checkpoint passed 3,467 tests. The `438f974`
baseline passed 3,352 before the compatibility harness and 3,415 after it. The
Abstractions snapshot now includes the six additive authorization evidence types
and the reviewed replacement of the incomplete four-argument artifact prepare
fingerprint with its complete eleven-argument contract. The artifact-store
snapshot includes the approved test-assembly metadata change. The conversational
provider fixes change no public API. External embedding API changes remain
outside these baseline approvals.

This verifies checkout reproducibility after `83888cc` repaired the broad
`artifacts/` ignore rule and captured the omitted artifact source and tests. The
earlier 3,440-test result came from the shared workspace and included
uncommitted provider embedding changes. Those changes are intentionally absent
from this committed API baseline and may produce expected API differences in the
shared workspace until their own checkpoint is reviewed.

The compatibility suite checks emitted public and protected API shape, not
behavior or internal XML documentation. It rejects missing, stale, or duplicate
assembly snapshots and requires explicit snapshot updates. Its extractor's
unsupported generic nullable extension-receiver shape is documented and fails
closed; current supported C# 14 extension shapes have a dedicated fixture.

Rich identity contracts, the first-party identity runtime, reduced test-fixture
migration, and pinned agent admission have verified checkpoints. Catalog
bootstrap and revision-preserving publication are also verified. Full keyed
run-plan composition, downstream identity revalidation, and caller-wait versus
authoritative-run cancellation remain open.

## Concurrent ownership

Concurrent tool-feature and provider embedding work was incorporated by Alex in
`dbf22d6`. The coordinating team preserved that commit, reviewed the provider
API changes, and included the combined tree in the `bd1101c` integration gates.
Future shared abstraction and solution edits continue to use narrow ownership
and explicit coordination.

## Active corrections

- Declared component graph validation and deterministic continuation/input
  policies are verified in `bd1101c`. Provider-bound registration capture and DI
  correspondence are now verified. First-party descriptor co-registration,
  complete component/profile selections, run-plan compilation and activation
  remain open. `IInputCoordinator` and `IInputQueue` still need their runtime
  implementations, including authorized replay before preprocessing. The
  in-memory session store now implements lane provisioning, idempotent
  admission, and atomic promotion into accepted-run state; connecting this state
  to actual execution remains open.
- Continuation distinguishes the previous committed turn from the next target
  turn, retains every pending cause, and requires authoritative terminal tool
  references and consistent active compaction evidence. The session owner must
  still prove those commits and revalidate proposals after asynchronous policy
  evaluation. The current loop does not yet call the policy.
- The artifact preparation contract clarified in `08c7bfc` is implemented in
  `1527997`; reusable store conformance is committed as `862f56d`. Durable
  reference commitment, retention coordination, and recovery remain open.
- Anthropic and Gemini HTTP failures and cancellation are corrected in
  `c765ee8`. Broader provider capability and protocol conformance remain open;
  the external embedding implementation has its own ownership and review.
- Security capture values, explicit keyed authority selection (`a3fd03b`), audit
  dispatch (`8af9a62`), and configured exact profile publication/capture have
  verified checkpoints. Facade admission now pins exact security/session
  publications and downstream session, loop, tool, compaction, and Plan requests
  carry captured evidence. Complete selected-graph activation, retained-policy
  lifetime, and remaining downstream binding coverage remain open. `6b0bb98`
  distinguishes pinned run configuration from exact per-operation authorization
  scope; possessing a context does not grant an effect. `260bf5b` specifies
  publication and activation ownership. A default selection reader depends on
  validated agent component selections and cannot be replaced by an unchecked
  profile-key registry.
- Output diagnostic bounds are corrected in `4737dcd`, text is bounded before
  parsing in `de4910b`, and schema values own detached JSON in `ad7ce18`.
  `11e80f9` adds declared vocabulary/dialect capabilities, keyed schema-engine
  selection, complete schema preflight, and typed non-retriable configuration
  failures. `211b0f8` bounds traversal before retaining children, rejects large
  tokens before decoding or writer allocation, and uses bounded canonical bytes
  for runtime conversion. Duplicate-name sets are built only after aggregate
  byte validation. `a054e1d` enforces retained output-definition and alternative
  invariants through construction and record copies. The active engine/loop does
  not yet resolve output definitions or invoke the output processor; complete
  run-plan selection must connect both before claiming end-to-end structured
  output. Synthetic-tool, media, and union processing, retry-budget integration,
  and the remaining replaceable output pipeline collaborators remain open.
- Filesystem capabilities now remain independently replaceable in `c566dc5`.
  Complete keyed profile composition, narrow read/write contracts and the
  deterministic in-memory backend remain open.

## Component coverage

The initial inventory covers all 27 architecture pages. “Unverified” means that
implementation exists but the complete contract has not been demonstrated.
“Missing” names a required owner absent from the inspected repository; it does
not make that component a mandatory dependency of every engine.

| Owner                         | Outstanding implementation or proof                                                                                                                                    |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Architecture index            | End-to-end source-of-truth conformance and complete coverage ledger                                                                                                    |
| Foundation contracts          | Validated values, behavioral compatibility, versioning and deterministic primitives; emitted API baseline established                                                  |
| Project structure             | Missing owners, declared graph activation, unchecked leaf protocol ownership and required project/test topology; project and pure component graph checks established   |
| Composition and configuration | Full closed runnable graph, catalog publication/reload, keyed selection, scope ownership, readiness                                                                    |
| Agent runtime                 | Explicit state transitions, waiter cancellation, recovery identity, settlement outcomes                                                                                |
| Budgets                       | Replaceable ledger/profile/policy/event contracts, consumer integration, durable accounting and full conformance                                                       |
| Messages and history          | Immutable/loss-aware values, non-elevation, correlation and shared round-trip conformance                                                                              |
| Input and output              | Admission, durable promotion, lane routing, fan-out, final publication and channel contracts                                                                           |
| Structured output             | Complete candidate extraction, validation, repair decisions and conversion conformance                                                                                 |
| Context                       | Instruction precedence, contributor trust/order, bounded assembly and request manifests                                                                                |
| Context compaction            | Safe cuts, trustworthy activation evidence, cancellation, cursor/manifest reconciliation                                                                               |
| Identity                      | Verified normalization/derivation baseline; downstream revalidation, ingress integration and reusable conformance                                                      |
| Model and embedding providers | All advertised operation/capability mappings, endpoint/account bindings, terminal/error/usage semantics                                                                |
| Tools                         | Authoritative terminal records, rejection projections, scheduling, retries and focused feature contracts                                                               |
| Permissions and human control | Policy algebra, grants, approval persistence/replay, selectors, required audit and bounded infrastructure bootstrap                                                    |
| Sessions                      | Lifecycle after accepted state, complete lane coordination, branch fencing, retention/export/import; missing SQLite backend                                            |
| Durable execution             | Missing runtime and explicit backend; journals, codecs, leases, checkpoints, evidence and recovery                                                                     |
| Memory and retrieval          | Missing runtime/storage ownership; documents/vectors, retrieval provenance, tombstones and purge                                                                       |
| Goals and delegation          | Durable goals/attempts/intents, joins, communication, parent occupancy and missing hosting worker                                                                      |
| Hooks and extensions          | Typed point coverage, ordering, mutation validation, failure precedence and timeout quiescence                                                                         |
| Observability                 | Complete safe signals, reusable assertions, required-sink separation and exporter implementation                                                                       |
| MCP                           | Supported protocol eras, reflection, transports/lifecycle, capabilities and protected primitive adapters                                                               |
| File system                   | Full dispositions/bounds/isolation semantics and missing deterministic in-memory backend                                                                               |
| Network                       | DNS/send authority, connection reuse, redirects/retries, bounded streaming and egress evidence                                                                         |
| Processes                     | Executable identity, sandbox enforcement, termination certainty, child effects and deterministic backend conformance                                                   |
| Artifacts                     | External ownership/reference shape, shared classification, event contracts, reference-commit evidence, integrity/retention, finalize/abort races and fenced collection |
| Testing and evaluation        | Additional reusable contract suites, compatibility snapshots and missing evaluation owner                                                                              |

## Dependency order

1. Establish reusable verification infrastructure and repair the baseline
   failure.
2. Complete the runnable composition, identity, session, loop and I/O contracts.
3. Integrate budget and security ownership at every protected/charged boundary.
4. Complete session persistence and durable execution, including crash/replay
   evidence across effects and terminal records.
5. Complete tools, host access, providers, MCP, artifacts, context and output
   against their shared suites and the cross-component acceptance matrix.
6. Complete goals/delegation, memory/retrieval, evaluation, exporters and the
   linked coding-harness composition/profile requirements.
7. Audit every architecture requirement against current implementation and test
   evidence; run the complete repository gates before claiming completion.

## Integration gates

- All source and test projects build under the required .NET/C# and analyzer
  rules.
- All deterministic unit and conformance suites pass, including rejection,
  cancellation, concurrency, lost acknowledgements and recovery boundaries.
- Public API changes have reviewed compatibility evidence and required XML docs.
- Project and declared service graphs obey the architecture; no hidden
  factories, permissive fallbacks, direct protected effects or fake capabilities
  bypass it.
- Each row in the
  [cross-component acceptance matrix](architecture/testing-and-evaluation.md#cross-component-acceptance-matrix)
  has an actual composed fixture and passing evidence.
- Required provider wire/capability fixtures and coding-harness profile
  scenarios pass. Live tests remain separately opt-in as specified.
- Final completion records the evidence for every component above; absent,
  partial, or merely unverified behavior remains open work.
