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

## Latest integration evidence

An isolated checkout matching committed `bd1101c` passed `make format`,
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
  policies are verified in `bd1101c`. Descriptor co-registration, complete
  component/profile selections, run-plan compilation and activation remain open.
  `IInputCoordinator` and `IInputQueue` currently describe contracts; authorized
  replay before preprocessing and atomic session-backed admission and promotion
  still need implementations.
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
- Security capture values are implemented, but profile/policy/authority
  catalogs, selectors, retained snapshots, and downstream request/grant binding
  remain open. `6b0bb98` distinguishes pinned run configuration from exact
  per-operation authorization scope; possessing a context does not grant an
  effect. `260bf5b` specifies publication and activation ownership. A default
  selection reader depends on validated agent component selections and cannot be
  replaced by an unchecked profile-key registry.
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
| Sessions                      | Lane operations, receipt-before-conflict replay, branch fencing, retention/export/import; missing SQLite backend                                                       |
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
