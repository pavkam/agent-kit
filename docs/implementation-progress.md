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

| Checkpoint                                             | Evidence                                                                                                                                                          | Result                                                                                                                                     |
| ------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| Authoritative architecture                             | `0c73575`; Markdown lint across 127 documentation/guidance files                                                                                                  | Committed                                                                                                                                  |
| Existing implementation baseline                       | `ef6582e`; solution build: zero warnings/errors; 2,821 existing tests run                                                                                         | Committed; 2,820 pass, one session-store activity-correlation failure; architecture conformance unproven                                   |
| Budget accounting and batch admission                  | Focused budgets: 67 passed; abstractions: 1,403 passed; atomic hierarchy, started/unknown preservation, batch replay and revisioned correction                    | Verified checkpoint; ledger/profile and consumer integration remain open                                                                   |
| Policy contribution validation and grant evidence      | Focused permissions suite: 23 passed; malformed policy contributions deny, cancellation propagates, replay/concurrency tested                                     | Verified checkpoint; broader permissions work remains open                                                                                 |
| Observation-test isolation and shared test support     | Focused Session.InMemory suite: 49 passed; source/correlation isolation, duplicate tags and helper validation                                                     | Verified checkpoint; shared support established                                                                                            |
| Reusable grant-store conformance and solution coverage | Focused permissions suite: 31 passed, including eight inherited contract cases; all 120 current projects registered in the solution                               | Verified checkpoint; other contract suites remain open                                                                                     |
| Session tenant isolation and replay evidence           | Focused Session.InMemory suite: 61 passed; foreign-tenant masking, structural replay, changed-request conflicts, original branch receipts and deletion tombstones | Verified checkpoint; identity authentication, grants, durable retention and lane coordination remain open                                  |
| Pinned-agent admission and retained-value integrity    | Focused facade suite: 59 passed; replacement/removal, reconstructed values, scope failure, schema ownership, one terminal admission and isolated diagnostics      | Verified checkpoint; keyed run-plan composition, retention/revocation catalogs and waiter cancellation remain open                         |
| Trusted execution identity and delegation              | Focused identity runtime: 58 passed; rich contracts and migration included in full 3,440-test integration pass                                                    | Verified checkpoint; downstream admission/revalidation and reusable identity conformance remain open                                       |
| Catalog bootstrap and publication                      | Focused facade/catalog: 91 passed; abstractions: 1,448 passed; full integration: 3,440 passed                                                                     | Verified checkpoint; no-I/O readiness, immutable revision bindings and atomic publication; complete selected graph validation remains open |
| Selected capability requirements                       | `5148df1`; focused contract tests: 13 passed                                                                                                                      | Removed best-effort resolution of a selected capability; omission is the optionality boundary                                              |
| Reproducible artifact contract capture                 | Clean archive exposed an ignored source directory; Abstractions build: zero warnings/errors; focused artifact contracts: 10 passed                                | Anchored build-output ignore rule and captured 38 existing contract files plus their tests; artifact conformance remains open              |
| Typed profile selection identities                     | `438f974`; 31 focused key tests                                                                                                                                   | Six missing profile keys added; complete selected graph remains open                                                                       |
| Public API baseline                                    | `9d2c6ed`; 60 snapshots, 63 focused checks, 3,415 clean solution tests                                                                                            | Reproducible baseline; behavioral compatibility remains a separate concern                                                                 |
| Identity narrowing conformance                         | `f053ff3`; full Identity suite: 73 passed, including 15 reusable cases                                                                                            | Prevents subject replacement and successive claim/assurance widening; downstream admission remains open                                    |
| Artifact grant evidence                                | `50293ac`; 15 helper checks, 9 coordinator tests, 12 store tests                                                                                                  | Versioned complete-reference binding and denial after reference mutation                                                                   |
| Project graph enforcement                              | `b903457`; 15 checks in Debug and 15 in Release                                                                                                                   | DAG and core inward edges enforced; constructor graphs and leaf-to-leaf ownership remain open                                              |
| Artifact tombstones and publication                    | `c8131ad`; full InMemory artifact suite: 18 passed                                                                                                                | Exact tenant/reference replay, immutable-version collision rejection and atomic publication winner                                         |

## Latest integration evidence

An isolated archive of committed `c8131ad` built all 124 solution projects with
zero warnings or errors and passed all 3,467 tests, with no failures or skips.
This includes the 60-package public API baseline, 63 compatibility checks, 15
package-graph checks, 73 identity tests, and 18 InMemory artifact store tests.
The graph suite also passed its 15 checks in Release.

The earlier isolated `438f974` baseline passed 3,352 tests before the
compatibility harness and 3,415 after it. Those original API snapshots remain
unchanged by the later behavioral fixes and passed in the final clean archive.

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

Two external agents own tool-feature completion and provider embedding support.
The coordinating team avoids their implementation and provider-contract files
and reviews their final changes at integration checkpoints. Shared abstraction
and solution edits use narrow patches and explicit ownership coordination.

## Component coverage

The initial inventory covers all 27 architecture pages. “Unverified” means that
implementation exists but the complete contract has not been demonstrated.
“Missing” names a required owner absent from the inspected repository; it does
not make that component a mandatory dependency of every engine.

| Owner                         | Outstanding implementation or proof                                                                                                                                    |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Architecture index            | End-to-end source-of-truth conformance and complete coverage ledger                                                                                                    |
| Foundation contracts          | Validated values, behavioral compatibility, versioning and deterministic primitives; emitted API baseline established                                                  |
| Project structure             | Missing owners, constructor/factory graph, unchecked leaf protocol ownership and required project/test topology; core project graph enforced                           |
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
