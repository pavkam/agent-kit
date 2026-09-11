# Architecture implementation progress

This is a work ledger, not a specification. The
[architecture](architecture/index.md) and its linked normative concepts and
profiles remain authoritative. A green existing test suite does not establish
conformance to requirements it does not exercise.

## Objective and operating rules

The active goal resumed broad architecture implementation on 2026-09-11,
starting with the high-level runnable composition and following its unfinished
lower-level dependencies. Implementations, tests and docs move together; each
verified checkpoint is committed and pushed. The September 9 closeout remains
historical evidence, not a completion claim for the architecture.

The objective is to implement all missing parts of the documented architecture
and correct divergent implementations. Preserve concurrent work and record
unresolved requirements rather than weakening specifications to match existing
code.

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

The retained projection-policy catalog implements exact-version lookup over
immutable configured snapshots. Resolved and unavailable outcomes retain the
requested reference, equivalent duplicate snapshots are idempotent, and
conflicting content rejects composition. Registration is replaceable, preserves
host clocks, and neither fabricates policy content nor builds a service
provider. Explicit snapshot replacement operates before capture and cannot
mutate an existing catalog.

The shared catalog conformance suite covers retained revisions, unavailable
keys/versions, ordinal matching, cancellation, and concurrent resolution through
public DI. Implementation tests cover collision handling, registration
replacement, source-collection mutation, safe diagnostic fields, parented
terminal activities, bounded metrics, and throwing observers/clocks. Value tests
exercise the closed resolution family with typed construction and copy paths.
Complete tool-result projection, canonical alias resolution, terminal recording,
message codecs, and durable output publication remain open.

Verification: the Tools suite passes 76 cases and Abstractions passes 3,470. All
6,791 Release tests pass without skips, with zero build warnings or errors. The
three reviewed API snapshots contain additive contracts, registration methods,
and shared diagnostic names; all 68 compatibility checks pass.

The projection-provenance checkpoint adds `ToolResultProjectionInfo` and the
closed `ToolResultProjectionLoss` vocabulary. Provenance retains the exact
captured policy reference, ordered and repeated loss evidence, and measured
nonnegative omission counts. Positive omissions without content-loss evidence
reject, including status-only claims. Reconstructed values compare structurally.

The 27 focused cases use direct typed construction in
`ToolResultProjectionInfoTests`. This additive value contract neither resolves
policy snapshots nor enforces content bounds. Canonical `ToolReference`, richer
`ToolCallOutcome`, integration into `ToolResultPart`, captured catalog
resolution, terminal recording/projection, and the admitted-input/message codecs
remain open before durable output publication can be completed.

Verification: all 6,742 Release tests pass without skips, with zero build
warnings or errors. The reviewed API snapshot adds only the two projection
provenance types, and all 68 compatibility checks pass.

The preceding fixture checkpoint (`4921ae2`) reorganized behavioral tests into
their production-class fixtures and replaced reflection-based construction with
typed factories and accessors. All 6,715 Release tests passed, together with
formatting and lint; production APIs were unchanged.

The final-result checkpoint adds the canonical accepted/rejected result and
stream-start unions, seven semantic outcomes, and independent settlement
outcomes. Finished envelopes validate cursor, history, usage and external
handoff correlation before capture. Clean success requires both semantic success
and completed settlement; recovery-required results retain their output and
usage. Legacy reduced-loop outcomes still require explicit migration.

All four closed families guard record-copy construction. Eight focused cases
reproduced foreign/null-copy acceptance before the guard; normal built-in and
legacy copies remain valid. The existing semantic outcome copy signature is
preserved, with deliberate rejection of previously constructible foreign
variants contrary to its documented closed-family contract. Tests in this
checkpoint now live in fixtures named for their production classes; the concrete
stream fixture inherits its shared conformance suite.

Immutable deferral requests capture continuation ownership, effect-start
evidence, normalized operations, exact input fingerprints and audited decision
references. Runtime-owned provider suspension cannot become a terminal external
handoff. The values validate structure; session persistence and authenticated
resolution remain separate unfinished work.

The internal event hub now supplies the public typed stream through a thin
adapter. Cancellation, abandonment and overflow affect event delivery while the
producer-owned completion remains independently awaitable. Final correlation is
checked before exposure, and completion waiting uses the existing isolated logs,
activities and bounded metrics. Four reusable conformance cases exercise the
public stream contract through this internal adapter; complete publisher
registration and DI conformance remain open.

`IOutputPublisher` now defines the required publication boundary. No partial
default publisher is registered. Durable sequence ranges, publication intents,
required sinks, settlement integration, deferral resolution, facade/loop
migration and complete keyed activation remain open.

Verification: all 6,597 Release tests pass without skips, with zero build
warnings or errors. The owning Abstractions and I/O suites pass 3,327 and 140
tests respectively, including shared stream conformance. The reviewed API
snapshot adds 30 result/deferral types and the external-handoff guard, and
records the now-explicit semantic-outcome copy constructor. All 68 compatibility
checks pass.

The run-usage checkpoint adds the immutable `RunUsage` dependency required by
progress and final-result envelopes. Each charged attempt retains distinct entry
identity, operation and model/request attribution, original provider usage,
measurement quality, pricing provenance, and replacement revisions. Equivalent
current replay is a no-op; stale, conflicting, skipped, foreign-run and
changed-attribution updates reject. Corrections replace rather than accumulate
prior usage, and previous snapshots remain unchanged.

Aggregates use exact quantities and explicitly selected dimension/unit
semantics. Missing observations remain unknown, currencies stay separate, and
concurrent gauges require explicit zero replacement after proven completion.
This is a current projection, not the append-only durable usage ledger or budget
authority. Session ledger persistence, provider/tool projection integration,
final-result and deferral runtime integration, settlement, and complete
publisher activation remain open.

Verification: all 6,407 Release tests pass without skips, with zero build
warnings or errors. The new values add 87 focused usage cases and 10 shared
identity-conformance cases. The reviewed API snapshot adds nine types without
changing existing signatures, and all 68 compatibility checks pass.

The I/O event-hub checkpoint implements the internal bounded fan-out mechanism
required beneath the output publisher. Recipient capture and strict sequence
validation are atomic. Subscriptions buffer before reading begins, support one
enumerator, and release capacity on cancellation or abandonment. A saturated
subscriber receives explicit delivery failure with the first unavailable
sequence; healthy subscribers continue. Normal completion retains the bounded
prefix for consumer-owned draining, while premature disposal fails delivery.

The focused I/O suite passes 117 tests, including 66 new cases for bounds,
correlation, sequence races, lifecycle ownership, cancellation, safe
diagnostics, and observer failure isolation. Shared activity/metric/tag names
are additive; no competing public event hub or partial publisher registration is
introduced. The hub limits event counts; payload-byte bounds remain an upstream
publisher responsibility.

The Release solution build passes with zero warnings or errors, and all 6,310
tests pass without skips. The reviewed API snapshot adds only the four shared
observability constants; all 68 compatibility checks pass.

Complete output publication remains open. Its missing prerequisites include
durable sequence-range reservation and publication intents, required sink
delivery, settlement and deferral execution, replay/resnapshot, and canonical
keyed activation. The internal hub does not claim those guarantees.

The resumed facade checkpoint validates core service cardinality from captured
DI descriptors before application activation. Missing or duplicate catalogs,
run-profile readers, security-profile selectors, grant stores, clocks, run and
operation identity generators, loops and facade registrations fail with stable
diagnostics. Standalone and hosted builds share this validation; disabling
Microsoft DI constructor validation does not disable it. Keyed alternatives do
not satisfy or conflict with an unkeyed requirement. Hosts using feature
packages without the facade remain independent of the runnable spine.

Tests cover the two build paths, keyed alternatives, missing services, aggregate
diagnostics, removal of the facade registration, and rejection logs, activities
and bounded metrics. Full keyed runnable composition, first-party descriptor
registration and run-plan activation remain open. Verification for this
checkpoint: the Release build passed with zero warnings or errors, and all 6,244
tests passed without skips, including 309 facade tests and 177 session tests.
Public API compatibility checks passed without snapshot changes.

The first full run exposed cross-test session diagnostics interference: an
unfiltered lease fault-injection listener also threw for activities sampled by
other tests. A deterministic two-case reproduction failed on the original
callbacks. Session listeners now restrict observation and fault injection to the
owning trace and operation; 177 focused session tests passed before final
integration. The tests retain active fault injection and verify unrelated
activities while the throwing listener remains installed.

The usage-report lifecycle checkpoint passed a Release build with no warnings or
errors and all 6,205 tests without skips in an isolated checkout over `da4cb59`.
`ModelUsage` now requires `NotReported`, `Interim` or `Final`, keeps unknown
fields distinct from reported zero, validates nonnegative known values, and
preserves cost and currency independently. An absent report carries no counter,
cost, currency or extension evidence; it cannot be published as a
`ModelUsageUpdated` event.

This is an intentional public API correction: callers supply the report state,
use `ModelUsage.NotReported` in place of `Empty`, and construct immutable values
instead of changing their properties through object initializers. The reviewed
API snapshot captures the constructor and accessor changes. Thirty-four new
cases cover value constraints and provider regressions alongside migrated
existing fixtures.

Provider mappings distinguish usage evidence from response framing. Tested cases
retain OpenAI final usage without `[DONE]` and after a malformed later chunk,
Anthropic interim usage without final usage evidence, and Gemini interim usage
after truncation. A stop-reason-only Anthropic event does not promote an earlier
report; Gemini and Mistral likewise retain nonterminal-only usage as interim.
Cohere rejects negative fractional counters before integer projection; Vertex
rejects negative per-item counts and overflowing batch totals. Malformed usage
becomes a typed protocol failure.

This finishes the three checkpoints already underway when the user stopped the
broad goal. Complete provider wire conformance, descriptor/runtime integration,
run-level accounting and the other open architecture requirements remain open.

The provider-compatibility checkpoint passed formatting, a Release build with no
warnings or errors, and all 6,171 tests without skips in an isolated checkout
over `a99e044`. Six additive values retain exact profile identity, positive
publication revisions, configured candidate multiplicity, usage-report
availability and ordered tool-schema dialect evidence. Thirty-seven additional
cases include shared identity conformance, invalid boundaries and structural
equality.

The owner and concept documents now specify the portable profile shape, reject
conflicting publication under one key and version, and distinguish report-phase
availability from actual usage evidence. The API snapshot was reviewed. These
values do not activate a wire profile, verify fingerprints, grant authority or
complete descriptor publication and runtime preflight; that integration remains
open under the stopped architecture goal.

The SQLite budget-ledger checkpoint passed formatting, a Release build with no
warnings or errors, and all 6,134 tests without skips in an isolated checkout
over `b3f9b89`. The explicit local adapter implements the shared ledger contract
with exact decimal accounting, atomic batch admission, stable replay receipts,
revisioned corrections and retained started reservations after process loss.
Shared scope-admission mechanics keep the in-memory and SQLite adapters aligned.

The gate covers concurrency, bounded codecs, exact schema and store identity,
corrupt persisted values, commit-acknowledgement uncertainty, indexed active and
unresolved queries, process-kill recovery, and diagnostics that cannot change
semantic outcomes. The additive public API snapshot was reviewed. SQLite
provides durable host-local storage; distributed ownership, cross-store
atomicity, full budget profile selection and consumer integration remain open.

The provider-profile and history-cursor checkpoint passed formatting, a Release
build with no warnings or errors, and all 6,038 tests in an isolated checkout
over `66c051d`. Fifteen provider-neutral values preserve exact endpoint and
credential profile identities, positive publication versions, operation bindings
and immutable snapshots. Constructors validate local shape without claiming
profile availability, destination safety, fingerprint verification or credential
authority. URI transport validation remains with the branded owner.

`MessageCursor` retains agent, session, optional conversation, branch, version
and append sequence. It rejects default identities while preserving zero and
independent watermark values. Eighty-five additional cases, including shared
identity conformance, cover defaults, optional values, bounds, copies and exact
correlation. The additive API snapshot was reviewed. Provider profile
publication, activation, protected credential leases, complete descriptors,
request execution and history-reader integration remain open.

The portable-error and side-effect-certainty checkpoint passed formatting, a
Release build with no warnings or errors, and all 5,953 tests in an isolated
checkout over `e86c153`. `AgentError` retains all ten normative fields with
exact local guards; extensible code and origin values preserve unknown machine
text. The 48 typed framework codes cover the stable taxonomy plus `Unknown`.
Mappers remain responsible for safe content and operation-specific error
translation.

Side-effect certainty preserves the original three numeric values and adds
partial completion and not-applicable states. Completion never proves durable
terminal recording. Tool terminal values reject not-applicable certainty and
treat partial mutating effects as possibly started for retry safety. Forty-eight
additional cases cover error values, numeric compatibility, recovery evidence
and tool replay constraints. The additive API changes were reviewed; rejecting
not-applicable tool certainty is an intentional boundary correction. Provider,
store and runtime error mapping, durable recovery and I/O final envelopes remain
open.

The run-event and context-provenance checkpoint passed formatting, a Release
build with no warnings or errors, and all 5,905 tests in an isolated checkout
over `900b186`. Extensible run events retain exact correlation, positive per-run
sequence and explicit live/durable classification. Built-in content deltas and
committed-message events require a turn and validate their request,
part-position, message and session-version evidence.

Context source references preserve exact namespace, key and textual revision;
contributor catalog versions are positive. Trust and evaluation-frequency values
retain the normative vocabulary without assigning authority or numeric
precedence. Sixty-six additional cases, including shared identity conformance,
verify defaults, guards, boundary values, extensibility and immutable copies.
The additive API snapshot was reviewed. Publisher sequence allocation, durable
fan-out, final envelopes, source resolution, instruction precedence and request
manifests remain open.

The budget-result closure checkpoint passed formatting, a Release build with no
warnings or errors, and all 5,839 tests in an isolated checkout over `f960ecb`.
Four documented closed result families now reject external variants constructed
through their inherited record-copy constructor. Valid built-in copies retain
their type and value; null originals reject explicitly. Twelve focused cases
exercise those boundaries, and the explicit protected API entries were reviewed.

This intentionally rejects an undocumented construction path that previously
bypassed each family's private-protected constructor. Extensible event and
provider families are assessed separately; this change does not impose closure
on every abstract record. The remaining closed-family audit stays open.

The effective-configuration and toolset value checkpoint passed formatting, a
Release build with no warnings or errors, and all 5,827 tests in an isolated
checkout over `7d79b52`. Toolset publications retain exact policy references,
ordered source selections and alias assignments. Effective snapshots retain
strictly ordered setting paths, typed semantic values and exact bidirectional
source/contributor provenance.

Sixty-seven additional cases cover value constraints, positive versions,
source/alias uniqueness, empty local snapshots, JSON ownership, structural
equality and valid record copies. Configuration's closed semantic family rejects
external copy-constructor bootstrapping. The additive API snapshot was reviewed.
These values validate local representation; source authentication, schema-aware
merge, completeness, retained publication, runtime capture and selected-graph
activation remain open.

The terminal-tool value checkpoint passed formatting, a Release build with no
warnings or errors, and all 5,760 tests in an isolated checkout over `80cba80`.
It adds complete accepted-call and terminal-result evidence, closed owned
content variants, exact optional usage, captured normalization policy and actual
normalization evidence. Unknown numeric statuses remain exact; unresolved
aliases retain no fabricated canonical identity or effects.

Fifty-nine focused cases plus shared identity conformance verify malformed
inputs, authorization correlation, nullable identity defaults, policy coherence,
copy ownership, structural equality and retry evidence. Preacceptance keyed
rejections may lack an external key; a potentially started mutating retry needs
compatible idempotency evidence. Constructors retain historical evidence without
reactivating grants or claiming aggregate-byte validation.

The additive API snapshot was reviewed. An explicit protected copy-constructor
guard prevents external records from bootstrapping another content variant,
while preserving valid built-in copies. Terminal wrappers also reject malformed
copies of legacy media and schema references. The wider closed-record and
legacy-reference audits remain open. Runtime acceptance/terminal recording,
normalization, deterministic projection, codecs, and provider/loop integration
remain open; these values alone do not execute or persist a call.

The model-selection candidate guard checkpoint passed formatting, a Release
build with no warnings or errors, and all 5,685 tests in an isolated checkout
over `7c9bca0`. Constructor and initializer/copy assignment now reject default
model aliases before retaining a candidate list. Three focused tests verify
exact exception names and valid candidate ordering. Public signatures remain
unchanged; this corrects invalid input previously accepted by the policy.

The configuration-source primitive checkpoint passed formatting, repository
lint, a Release build with no warnings or errors, and all 5,682 tests in an
isolated checkout over `5cb8982`. Source and path identities preserve ordinal
text, source revisions are positive, and immutable source references retain
explicit layer, trust, revision and fingerprint evidence. Uninitialized text
identities expose nullable values and format safely as empty strings.

Ten focused cases and the shared identity suite verify exact guards, defaults,
boundaries, equality and retained fields. The additive API snapshot was
reviewed. These values carry evidence; they do not authenticate sources, grant
authority, verify hashes or compile setting paths. Effective configuration
publication, merge, retention and run capture remain open.

The configuration and toolset architecture checkpoint passed focused Markdown
formatting and lint. Effective configuration now has a complete immutable
semantic shape, source/contributor provenance, captured precedence, and typed
selection cases. Constructors check local representation; schema-aware compilers
prove completeness before a publication authority retains the exact snapshot.
Untrusted sources cannot assign their own trust or widen managed constraints.

Toolset publications explicitly select sources and exact alias targets. Runtime
capture pins each discovered source version and its owned or borrowed invoker
bindings; it does not require authors to predict a future dynamic version.
Collision policy may select only evidence already present. This resolves the
owner-spec gaps blocking canonical run catalog integration; the configuration
compiler/publisher, toolset capture and provider/loop consumption remain open.

The immutable catalog-snapshot checkpoint passed formatting, repository lint, a
Release build with no warnings or errors, and all 5,655 tests in an isolated
checkout over `bb2ccc9`. `ToolCatalogSnapshot` retains exact run, identity,
security, definition, configuration and catalog evidence together with ordered
descriptors, their exact policy references, and provider aliases.

Twelve focused cases verify local guards, exact policy coverage, alias targets,
empty catalogs, ordered descriptors, map-independent equality, and normalization
of caller-supplied key/value comparers before collision checks. Multiple aliases
may target one identity; unaliased descriptors remain representable. The
additive API snapshot was reviewed. This value does not acquire invokers,
validate live authority, or complete engine/loop capture; those runtime
integrations remain open.

The coherent catalog-resolution checkpoint passed formatting, repository lint, a
Release build with no warnings or errors, and all 5,643 tests in an isolated
checkout over `4db57f7`. The catalog reads each tool descriptor once, advertises
that exact value, and returns it with the original borrowed tool. The invoker
authorizes that pair through the interface contract, including replacement
catalogs, without rereading live descriptor metadata.

Seven additional tests cover descriptor changes to effects/source/schema,
duplicate and null registrations, borrowed instance identity, custom catalog
replacement, invocation forwarding, and reusable paired-resolution conformance.
The old tool-only overload remains; the required paired overload intentionally
changes catalog implementor compatibility. Two API snapshots were reviewed.
Run-bound catalog capture, provider-alias resolution, authoritative terminal
recording, and deterministic projection remain open.

The terminal-result architecture amendment passed focused Markdown formatting
and lint. It separates bounded raw admission, authorized invocation acceptance,
authoritative terminal recording, and history projection. Unresolved aliases
retain no fabricated tool identity or effects. Accepted calls retain validated
argument and invocation-grant evidence; terminal records independently retain
any issued grant even when acceptance recording failed.

Captured run-level rejection policy supplies bounds before tool resolution.
Normalization records actual transformations and measured or unknown counts
separately from later projection losses. The closed content family owns its
payloads, usage remains optional exact evidence, and unknown numeric status is
preserved without implying success. Constructors enforce local consistency;
recorders compare retained cross-record evidence. This is a normative contract
checkpoint; its production values and runtime pipeline remain in progress.

The ledger-descriptor checkpoint passed formatting, repository lint, a Release
build with no warnings or errors, and all 5,636 tests in an isolated checkout
over `9ea2cf5`. Every `IBudgetLedger` now publishes immutable, side-effect-free
durability and concurrency-domain evidence. InMemory declares ephemeral,
process-local behavior; durability and concurrency remain independent claims.
The first-party budget authority rejects a missing descriptor before acquiring
loggers or performing ledger work.

Ten additional cases cover all valid descriptor combinations, exact enum/null
guards, stable adapter metadata, and reusable descriptor conformance. The
required interface property is an intentional implementor compatibility change;
two API snapshots were reviewed. Profile compatibility validation, SQLite
accounting, and complete consumer integration remain open.

The complete tool-descriptor checkpoint passed formatting, repository lint, a
Release build with no warnings or errors, and all 5,626 tests in an isolated
checkout over `05af3a5`. Descriptors now require exact tool version and source,
owned dialect-bound input schema, optional output schema, effect declarations,
and scheduling hints. Get-only properties preserve the validated shape. Optional
evidence distinguishes unasserted values from explicit empty or false claims;
mutating tools cannot claim read-only idempotency.

All 17 first-party declarations now carry explicit draft 2020-12 schemas and
stable package sources. Fifteen existing `1.0` versions are preserved; Read and
Write now publish their previously absent `1.0` versions. Their open root
schemas remain open. No output schema, retry mechanism, resource-kind claim, or
parallel safety is invented. Tests cover owned schema lifetime, exact guards,
structural effect evidence, scheduling-key consistency, and the affected feature
packages.

The constructor/property API correction is intentional and has a reviewed
snapshot; callers must supply actual versions, source identities, and owned
schemas. Complete catalog capture/resolution, authoritative terminal recording,
projection, and provider/loop migration remain open. The later
coherent-resolution checkpoint fixes the legacy catalog and invoker descriptor
rereads; full run-bound catalog capture remains open.

The extension-comparer correction passed formatting, repository lint, a Release
build with no warnings or errors, and all 5,620 tests in an isolated checkout
over `6c2091a`. `ExtensionData` now owns ordinal key comparison and the default
structural value comparer during construction and initializer/copy assignment.
Key spelling and value bytes remain unchanged. Equality is symmetric across
source comparers, and deriving a changed dictionary cannot silently retain an
old value because of a caller-supplied comparer.

Seven focused bag tests cover equality/hash consistency, exact null guards,
initializer ownership, and adversarial key/value comparers. Projection-policy
contracts now assert eager rejection at the extension boundary. Public member
signatures remain unchanged; default-value and serialized-content constraints
are separate remaining work.

The ledger-backed budget runtime checkpoint passed formatting, repository lint,
a Release build with no warnings or errors, and all 5,613 tests in an isolated
checkout over `22cf16f`. The first-party authority and immutable
scope/reservation handles now delegate accounting, expiration, identity
allocation, replay, and settlement to the explicitly selected `IBudgetLedger`.
Registration chooses no storage adapter or identifier generator. Selecting this
authority requires one unkeyed ledger; replacing it with a custom authority
remains independent.

Typed held results preserve boundary hold evidence. Exact ledger receipts flow
through commit and correction; disposal releases only unstarted work and leaves
started unknown spend retained. A failed release can be retried on the same
handle, including uncertain acknowledgement, with the same reference and no
caller cancellation token. Safe runtime activities, bounded metrics, and typed
log categories survive throwing listeners and logger factories. The 31 runtime
cases cover delegation, cancellation, refusal, composed in-memory behavior, and
retry forwarding; accounting conformance remains owned by the ledger suite.

Two additive held-result types and shared diagnostic names have reviewed API
snapshots. Ordinary scopes no longer falsely implement `IRunBudget`; explicit
run-profile integration must supply that specialization. Old runtime-owned
accounting and its duplicated tests are removed. Activation-time ledger
selection is verified; feature-owned pure readiness, profile/policy
requirements, operator security enforcement, consumer integration, and SQLite
persistence remain open.

The shared-schema checkpoint passed formatting, repository lint, a Release build
with no warnings or errors, and all 5,650 tests in an isolated checkout over
`89708cc`. `JsonSchemaDialectId` replaces `OutputSchemaDialectId` across output
profiles, preflight evidence, and validation without changing configured dialect
text or selection behavior. This is an intentional public type rename; callers
must update references, with no implicit compatibility conversion.

The new `JsonSchema` value captures an explicit dialect and owns a cloned object
or boolean document. It rejects invalid roots and conflicting, repeated, or
non-string root dialect declarations. It preserves ordinary duplicate fields and
unknown keywords for later engine preflight; construction does not claim schema
support or perform reference resolution. Tests cover exact guards,
disposed-source lifetime, retained content, and structural equality. The full
output suite passes; complete tool descriptors and catalog integration remain
open.

The tool identity and projection-policy value checkpoint passed formatting,
repository lint, a Release build with no warnings or errors, and all 5,630 tests
in an isolated checkout over `3451ddf`. Fourteen additive values represent exact
aliases, source/catalog versions, canonical tool pairs, execution-policy
references, and bounded versioned result-projection policy snapshots. They
preserve ordinal identities and validate positive versions/bounds, supported
transformation combinations, and initialized nested extension evidence.

The focused catalog and projection-policy suites contain 41 cases, including
exact exception types/parameter names, numeric boundaries, all known flag
combinations, and structural equality. Catalog capture/resolution, complete tool
descriptors, authoritative terminal recording and projection, and provider/loop
migration remain open. These values provide prerequisites without fabricating
runtime tool identity or default policy selections.

The accepted-operation codec checkpoint passed formatting, repository lint, a
Release build with no warnings or errors, and all 5,543 tests in an isolated
checkout over `1966dd5`. The additive public codec preserves complete accepted
run state at exact schema `1`, including lane, admission and history identities,
retained configuration, ordered recovery evidence, and timestamp offsets.
Identity and authorization evidence are encoded explicitly and reconstructed as
immutable values; decoding never resolves authority or mints a grant.

Payload, nesting, unknown-field and raw-extension bounds apply to the complete
security graph. Tests exercise exact byte and depth limits, malformed text,
inconsistent recovery references, validation before diagnostic effects, original
wire retention, and the shared codec conformance suite. Admitted-input and
message codecs, the mandatory durable base profile, and SQLite session
persistence remain open. The message codec also requires correcting incomplete
tool identity and result-projection contracts before their shape is persisted.

The captured budget-overrun-hold checkpoint passed formatting, repository lint,
a Release build with no warnings or errors, and all 5,524 tests in an isolated
checkout over `a754b24`. Each charged boundary retains its own overrun policy
and exact hold generation. Truthful settlement and correction atomically update
accounting and hold evidence; a held batch returns typed facts without reserving
capacity or inventing a numeric limit failure.

Automatic clearance waits for reconciled accounting. Operator clearance uses the
same eligibility checks and retains the exact structurally bound enforcement
receipt. Exact request replay preserves its original outcome, and a fresh key
cannot resolve a terminal generation or clear a later generation. The ledger is
authorization-neutral: authenticated selection and grant consumption remain the
runtime resolver's responsibility. Receipt equality compares ordered contents
across independently reconstructed arrays.

The new required `IBudgetLedger.ResolveOverrunHoldAsync` method intentionally
breaks custom ledger implementations until they implement it. Exhaustive batch
result consumers must handle `BudgetLedgerBatchReserveHeld`. Existing admission,
commit, correction, and snapshot constructors remain available with explicit
default policy or absent optional evidence. The 56 ledger tests and 2,487
abstraction tests cover replay, mixed ancestor policies, cancellation, target
binding, safe diagnostics, and value guards. Runtime migration, operator
security enforcement, complete profile/policy selection, and SQLite persistence
remain open.

The first portable session-codec checkpoint passed formatting, repository lint,
a Release build with no warnings or errors, and all 5,463 tests in an isolated
checkout over `1d533cc`. Explicit JSON codecs cover execution-lane provisioning
and input promotion at their exact schema `1`. They preserve identities, ordered
admission evidence, canonical wire values, and original envelopes containing
unknown fields without activating CLR types from stored data.

Captured per-codec limits bound actual encoded bytes, depth, unknown-field
counts, and raw extension bytes before materialization. Malformed UTF-8,
unpaired Unicode escapes, duplicate fields, inconsistent values, and copied
invalid base entries reject with typed outcomes. Tests exercise exact byte/depth
boundaries, validation before diagnostic effects, and independent parallel
listeners. The two public codecs and two shared trace tags are additive API
changes. Accepted-operation, admitted-input, and message codecs, the mandatory
durable base profile, and SQLite session persistence remain open.

The in-memory budget-ledger checkpoint passed formatting, repository lint, a
Release build with no warnings or errors, and all 5,405 tests in an isolated
checkout over `003e824`. The explicit `AgentKit.Budgets.InMemory` leaf stores
atomic hierarchical batches, original replay receipts, start evidence,
settlement, revisioned corrections, and unresolved-usage reconciliation. Exact
quantities preserve aggregate overrun; sum, duration, maximum, and live gauge
dimensions retain their distinct semantics. Mixed units reject before
accounting, and missing or foreign references remain indistinguishable.

The adapter's 48 tests include reusable conformance, concurrent parent capacity,
watermark paging, cancellation before mutation, exact replay after correction,
expiry, and isolated diagnostics with normal test parallelism. Its state is
ephemeral. Captured overrun-policy holds, migration of the runtime authority to
ledger-backed handles, complete store selection, and SQLite persistence remain
open; this storage checkpoint does not claim complete budget enforcement.

`BudgetStartExpired` now carries the reservation identity and persisted deadline
instead of fabricated numeric limit evidence. Both implementations return that
outcome for expiration; exact retries preserve the deadline. An explicitly
released reservation remains a distinct invalid start state. The legacy runtime
now throws `InvalidOperationException` for that misuse instead of returning its
previous misleading limit rejection. Exhaustive start-result consumers must
handle the additive expiration subtype.

The exact-budget-quantity checkpoint passed formatting, repository lint, a
Release build with no warnings or errors, and all 5,353 tests in an isolated
checkout over `f66c39f`. `BudgetQuantity` preserves nonnegative base-ten
aggregates beyond decimal magnitude and precision with canonical equality,
hashing, comparison, addition, and invariant formatting. Decimal conversion is
explicit and exact: unrepresentable values return a failed projection or throw
the documented overflow exception.

Four aggregate properties intentionally change from `decimal` to
`BudgetQuantity`: reserved/committed snapshot usage and observed/requested limit
failure amounts. Decimal constructors remain and retain their parameter-name
validation contracts. Per-reservation actuals and configured ceilings remain
decimal values. This corrects the public representation; the legacy authority
still awaits migration to the ledger. Budget and provider diagnostics tests now
filter their own operation and parent trace, closing observed parallel-listener
races instead of relying on retries.

The session-codec catalog checkpoint passed repository lint, a Release build
with no warnings or errors, and all 5,332 tests in an isolated checkout over
`ffc3c42`. The catalog captures descriptors once, selects exact local types and
wire schemas, preserves unknown entries as opaque bytes, and validates codec
results before returning them. A global payload cap applies before any decode;
known codecs also enforce their tighter descriptor cap. Codec-specific depth and
extension limits remain the codec's responsibility.

The catalog uses an injected clock and bounded diagnostics. Throwing listeners,
loggers, meters, and timing providers cannot replace results or exceptions;
activity failures restore the caller's parent trace. Tests isolate their own
operation and parent so concurrent session traces cannot contaminate assertions.
This checkpoint adds no concrete entry serializers, mandatory persistent-store
codec profile, or SQLite session adapter.

The budget-value checkpoint passed 2,411 abstraction tests, 67 budget-runtime
tests, and 66 public API checks in an isolated checkout over `9d6daed`.
Construction and record initialization now reject invalid limit dimensions,
units, negative amounts, undefined kinds, default parent identities, default
replay keys, and duplicate dimensions. The reusable type-qualified limit guard
has direct boundary and parameter-name coverage. This intentionally tightens
invalid-input behavior: malformed values fail at construction or copying, before
a ledger request can carry them. The only additive public API is the canonical
guard.

The budget owner document, concept, and skill now define amount-based gauge
accounting and distinguish maximum aggregation from addition. The existing
budget authority still needs migration to the new ledger; correcting the
contract does not claim that its legacy uniform-sum behavior is compliant.

The session-codec value checkpoint passed all 2,391 abstraction tests and 66
public API checks in an isolated checkout over `910e4d3`. The additive codec
contract uses exact wire type/schema identities, structurally equal immutable
payload bytes, and separate decoded, opaque, and rejected outcomes. A decoded
entry retains its original wire envelope so unchanged persistence can preserve
unknown nested fields. Encoding a new entry does not claim lossless rewriting.
The focused tests cover argument names, byte equality and hashing, wrapper
ownership, and arbitrary-byte retention. Bounds and semantic validation remain
codec responsibilities; these values neither validate stored authority nor
implement catalog dispatch, concrete codecs, or SQLite session storage.

The protected semantic-operation context checkpoint passed all 2,371 abstraction
tests and 66 public API checks in an isolated checkout over `709b20b`. The
additive value binds the complete execution identity, agent, optional session
and conversation, exact before/in/after-run correlation, and captured
authorization. Construction rejects mismatched evidence; get-only properties
prevent inconsistent record copies. Fifteen focused cases include truthful
sessionless before-run and after-run work. This adds provider-neutral operation
evidence, not a grant, provider executor, or retry implementation.

The loop-readiness checkpoint passed formatting, lint, Release build, and all
5,248 tests in an isolated checkout over `5375f15`. Build and hosted facade
resolution now inspect the captured loop registration without invoking its
factory or constructing a temporary loop. The current runtime requires exactly
one unkeyed loop; missing, keyed-only, and duplicate registrations reject.
Microsoft DI still validates constructor dependencies and lifetimes without
constructing the loop. Tests also prove that readiness does not dispose a
supplied instance and a scoped factory runs only when execution starts.

This corrects build-time activation. Complete declared factory metadata and
canonical keyed activation remain open. The dependency audit found missing
output event/result publication, model execution context, and top-level tool
execution contract families beneath `AgentComponentSelection`. Hook activation
and rich context assembly are further prerequisites. No temporary selection
shape or empty contract was added to bypass those requirements.

The Permissions SQLite checkpoint passed formatting, lint, Release build, and
all 5,242 tests in an isolated checkout over `0a60cb3`. The explicit
`AgentKit.Permissions.Sqlite` leaf persists grants, remaining uses, revocation,
and exact enforcement-intent receipts behind `ISecurityGrantStore`. Schema and
store identity checks share the mutation transaction; bounded binary codecs
preserve complete evidence, UTF-16 code units, and timestamp offsets. Reopening
a store reconciles an existing receipt without granting a second effect.

Both Permissions storage leaves use explicit additive selection. Repeating the
same leaf is idempotent; competing leaves or custom stores remain visible for
composition rejection. SQLite requires a host-supplied fixed target and explicit
initialization. It creates no parent directories, retains records indefinitely,
and promises local transactional storage without distributed fencing or OS
isolation. Uncertain acknowledgements require exact-intent reconciliation; the
legacy operation without an intent cannot safely retry automatically. Its 70
adapter tests include shared conformance, restart, corruption, concurrent use,
argument bounds, and isolated safe diagnostics with normal test parallelism.

The DI infrastructure validation checkpoint passed formatting, lint, Release
build, and all 5,170 tests in an isolated checkout over `b97049d`. Declared
component dependencies may explicitly opt into inspection of captured Microsoft
DI type and instance registrations. Validation closes generic registrations,
checks constructor dependencies, collections, cycles, and lifetimes, and
enforces a configurable bound without invoking factories or constructing
application services. Keyed metadata never invokes application key equality. The
existing explicit-declaration behavior and grant-store cardinality checks remain
intact. This adds infrastructure evidence; complete runnable-graph declarations
and required-spine coverage remain open work.

The budget-ledger contract checkpoint passed formatting, lint, Release build,
and all 5,106 tests in an isolated checkout over `4027060`. `IBudgetLedger`
defines atomic scope creation, ordered batch reservations, start, settlement,
correction, release, and recovery reconciliation. Immutable receipts retain
original requests and effective expiry; scope admission captures its limits and
lifetime. Exact address references, watermark paging, replay conflicts, and
uncertain persistence acknowledgements have explicit contracts. Focused tests
cover constructor constraints, complete ordered value equality, canonical
guards, and paging/receipt coherence. The current budget runtime still contains
its in-memory implementation; runtime proxies and explicit InMemory and Sqlite
leaves remain open work.

The session-codec descriptor checkpoint passed formatting, lint, Release build,
and all 4,995 tests in an isolated checkout over `861611c`. It adds a stable
entry wire identifier, finite payload/extension/depth limits, and immutable
descriptors binding an exact local entry type to explicitly readable schema
versions. Declared versions must include the write version; unknown version
ordering never implies compatibility. Ordered declaration equality, invalid
defaults, and the public version guard have focused tests. Codec catalogs, wire
envelopes, built-in codecs, and Session.Sqlite remain open; these descriptors do
not implement persistence or semantic recovery.

The enforcement value-equality checkpoint passed formatting, lint, Release
build, and all 4,952 tests in an isolated checkout over `3ab26b1`.
`SecurityEnforcementRequest` now compares ordered resource contents and hashes
the same complete evidence. Independently reconstructed requests and durable
intent receipts retain value equality; changed scope, identity, captured
authorization, audience, operation, effect, resource order, fingerprint, or
revocation evidence remains distinct. Existing default-array record copies have
total equality and hashing without gaining validity for enforcement. The
behavioral correction changes no accepted security authority. SQLite storage and
the budget-ledger extraction remain in progress.

The Permissions storage checkpoint passed formatting, lint, Release build, and
all 4,949 tests in an isolated checkout over `05fdae4`.

The Permissions storage checkpoint separates `InMemorySecurityGrantStore` into
`AgentKit.Permissions.InMemory`, with its own test project and explicit
`AddInMemorySecurityGrantStore()` registration. `AgentKit.Permissions` now
consumes `ISecurityGrantStore` without registering a concrete backend or
referencing a storage leaf. Application and test compositions select storage
explicitly; the grant-store conformance fixture uses the public leaf
registration. Grant consumption keeps the existing shared observation names and
event IDs.

Facade composition rejects missing or multiple unkeyed grant stores from its
captured service descriptors before building the provider or invoking any
application factory. Keyed-only stores do not satisfy this singular contract; a
null DI key follows actual unkeyed DI semantics. This adds required-store
cardinality validation while complete runnable graph validation remains open.

One enforcement intent can consume one exact grant only. A store-wide atomic
receipt index rejects reuse across grants, including concurrent callers, while
retaining the losing grant's capacity. Historical reconciliation validates the
presented grant and complete enforcement evidence. Cancellation observed after
the injected clock leaves both the use and receipt unchanged. Shared conformance
now covers the cross-grant cases for the upcoming SQLite adapter as well.

This intentionally moves the public implementation type and its namespace. Hosts
add the new package and registration, or supply another `ISecurityGrantStore`.
The old package contains no forwarding type or dependency on the new leaf.
Public API snapshots record the removed core type and the new adapter surface.

The storage policy now applies across the architecture and linked concepts:
concrete stores live in explicit `.InMemory`, `.Sqlite`, or other backend
leaves. Shared conformance proves common behavior; capability-specific checks
must prove restart durability and transaction guarantees. Immutable catalogs,
invocation caches, and local gates remain runtime mechanics. Session-backed
plan, input, goal, and settlement projections reuse the selected session
abstraction and its transaction boundary.

Enforcement-fence documentation now binds the fence to ownership required by the
selected action. An absent fence does not describe storage locality or weaken
grant, receipt, or audit enforcement. Protected journal ingress remains
unimplemented; this clarification does not claim otherwise.

SQLite grant storage is the next implementation checkpoint. Budget ledger
extraction and SQLite adapters for the other required storage families remain
open; the architecture policy does not imply those implementations already
exist.

The durability payload checkpoint passed complete formatting, lint, Release
build, and test gates in an isolated checkout over `43501c0`. Its additive API
snapshot change exposes the source-authored equality and hash members that now
implement the previously documented value contract.

The session store routing observation test now correlates both activity and
metric callbacks with its own parent span, snapshots measurements safely, and
proves that unrelated selections cannot satisfy its assertions. This fixes a
parallel-test race found by the complete gate.

`OperationPayload` compares schema identity and byte contents, so independently
allocated equal payloads work as dictionary keys and set members. Schema,
length, or byte differences remain unequal. Default schema versions reject in
construction and copies. Recoverable operation declarations also reject default
names, versions, and idempotency keys before assignment in both constructor
forms and record copies. Rejected copies leave the original values intact. These
checks do not add a durable journal, storage adapter, or recovery worker.

The captured-security integrity repair passed complete formatting, lint, Release
build, and test gates in an isolated checkout over `75cf59d`. It restores
validation removed by that refactor without changing the public API. Captured
requests, grants, and enforcement values reject null or contradictory scope and
identity copies before assignment. Grants also reject a policy version that
differs from their captured policy snapshot, in construction and copies.
Counterexamples against the prior commit demonstrated that matching
contradictory grant and enforcement copies could otherwise be registered and
consumed.

Grant-consumption faults again preserve the original exception and record an
error activity, bounded fault outcome metric, and safe event `5027`. A throwing
clock regression verifies that the failed attempt consumes no use and a later
valid attempt succeeds. Existing audit-test concurrency improvements and service
registration order remain intact. No dependency or test-runner workaround was
needed: the unchanged standalone Permissions test project builds and runs from a
fresh archive. Full captured-authority enforcement at every protected boundary,
required-audit settlement, and durable recovery remain open.

The protected session-coordinator and lane-ownership checkpoint passed complete
formatting, lint, Release build, and test gates in an isolated checkout over
`f943730c`. Two public API snapshots were reviewed, including the intentional
migration from session-wide process ownership to exact tenant/lane ownership.

`SessionExecutionCapability` retains the selected session profile and exact
coordinator instances for an invocation. Protected input lookup, lane provision,
admission, run acceptance, and state loading require that capability and verify
the receiving coordinator. Each transaction uses the protected directory and a
distinct fresh store authorization. Cancellation is checked after asynchronous
boundaries, including noncooperative collaborators. Legacy implementations get
guarded, cancellation-preserving unsupported results for the five new methods.

The default run coordinator keys local ownership by tenant, session address, and
execution lane. It validates canonical accepted state after obtaining a
provisional gate and publishes an owner only after the complete capture matches.
Busy requests also pass protected state validation before receiving identities;
another principal, stale capture, or provisional owner cannot disclose active
operation IDs. Other lanes and tenant partitions proceed independently. Exact
lease disposal cannot release a successor, and distributed-fencing requirements
reject before local ownership changes.

`ISessionRunCoordinator.AcquireAsync` now requires the compiled capability.
Lease requests carry the lane-bound in-run context and expected state revision;
leases expose immutable tenant/lane/operation/run/revision evidence. Busy
results require both operation and run identities. These intentional contract
changes require callers and replacement coordinators to migrate together. A
selected profile controls wait/reject behavior under the configured wait
ceiling. Zero wait is an immediate probe driven without a timer. Reusable
conformance covers same-lane exclusion, exact reacquisition, separate lanes, and
tenant collisions.

The capability is selection evidence, never a security grant. Actual keyed
capability compilation, legacy history/branch coordinator migration, transitions
beyond accepted state, loop driving, required-audit settlement, and distributed
ownership remain open. This checkpoint does not attest to complete
runnable-agent composition or crash-safe ownership from a process-local lease.

The durable-authorization binding checkpoint passed complete formatting, lint,
Release build, and test gates in an isolated checkout over `6a9d02c`. Its
reviewed API migration replaces scope-only durability authorization with the
complete captured `SecurityAuthorizationContext`. Agent-definition and
configuration revisions derive from that capture rather than independent fields.

`DurableOperationBinding` validates agent, session, operation, run, and optional
turn coordinates against the captured scope. In-run bindings retain the exact
active correlation. After-run bindings retain the causal run with no turn and
never reclaim active-run ownership. The current address cannot represent
before-run or sessionless work; those shapes reject explicitly. Zero-valued
durability-profile and agent-definition revisions remain valid.

Descriptors, checkpoints, results, and recovery evidence retain a single
binding. Their existing address/context constructor pairs now validate that
binding; new overloads accept it directly. Independent address/context
initializers and the old scope-only context constructor intentionally break
compatibility because they could retain mismatched or incomplete authorization
evidence. Callers must supply the complete accepted capture and construct a new
binding when coordinates change. Recovery evidence also rejects a checkpoint
from another binding, including a different tenant or durability selection, in
construction and copies. Reconstructed structurally equal bindings remain
accepted.

This verifies durable value contracts only. Protected journal and lease ingress,
trusted recovery admission, runtime activation, concrete journal
implementations, and required-audit settlement remain open.

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
failed copies, and preservation of the original value. Captured authorization
and address/correlation coherence are addressed by the later binding checkpoint;
runtime activation, journal implementation, and required-audit settlement remain
open.

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
| Budgets                       | Profile compatibility, policy/event contracts, consumer integration and full conformance; SQLite ledger checkpoint verified                                            |
| Messages and history          | Immutable/loss-aware values, non-elevation, correlation and shared round-trip conformance                                                                              |
| Input and output              | Admission, durable promotion, lane routing, fan-out, final publication and channel contracts                                                                           |
| Structured output             | Complete candidate extraction, validation, repair decisions and conversion conformance                                                                                 |
| Context                       | Instruction precedence, contributor trust/order, bounded assembly and request manifests                                                                                |
| Context compaction            | Safe cuts, trustworthy activation evidence, cancellation, cursor/manifest reconciliation                                                                               |
| Identity                      | Verified normalization/derivation baseline; downstream revalidation, ingress integration and reusable conformance                                                      |
| Model and embedding providers | All advertised operation/capability mappings, endpoint/account bindings, terminal/error/usage semantics                                                                |
| Tools                         | Authoritative terminal records, rejection projections, scheduling, retries and focused feature contracts                                                               |
| Permissions and human control | Policy algebra, approval persistence/replay, selectors, required audit and bounded infrastructure bootstrap; SQLite grant-store checkpoint verified                    |
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
