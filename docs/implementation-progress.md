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

| Checkpoint                                             | Evidence                                                                                                                                       | Result                                                                                                   |
| ------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| Authoritative architecture                             | `0c73575`; Markdown lint across 127 documentation/guidance files                                                                               | Committed                                                                                                |
| Existing implementation baseline                       | `ef6582e`; solution build: zero warnings/errors; 2,821 existing tests run                                                                      | Committed; 2,820 pass, one session-store activity-correlation failure; architecture conformance unproven |
| Budget accounting and batch admission                  | Focused budgets: 67 passed; abstractions: 1,403 passed; atomic hierarchy, started/unknown preservation, batch replay and revisioned correction | Verified checkpoint; ledger/profile and consumer integration remain open                                 |
| Policy contribution validation and grant evidence      | Focused permissions suite: 23 passed; malformed policy contributions deny, cancellation propagates, replay/concurrency tested                  | Verified checkpoint; broader permissions work remains open                                               |
| Observation-test isolation and shared test support     | Focused Session.InMemory suite: 49 passed; source/correlation isolation, duplicate tags and helper validation                                  | Verified checkpoint; shared support established                                                          |
| Reusable grant-store conformance and solution coverage | Focused permissions suite: 31 passed, including eight inherited contract cases; all 120 current projects registered in the solution            | Verified checkpoint; other contract suites remain open                                                   |

| Session tenant isolation and replay evidence | Focused Session.InMemory suite:
61 passed; foreign-tenant masking, structural replay, changed-request conflicts,
original branch receipts and deletion tombstones | Verified checkpoint; identity
authentication, grants, durable retention and lane coordination remain open |

## Latest integration evidence

At checkpoint `41f43f2`, the current shared worktree built all 120 registered
projects with zero warnings or errors. The complete solution test run passed
3,197 tests with no failures or skips. This includes the concurrent, uncommitted
provider embedding changes present during that run; it is not evidence that the
remaining architecture has been implemented.

Active next checkpoints are the rich identity contracts and first-party identity
runtime, followed by migration of the reduced identity test fixtures, and pinned
agent-definition admission revalidation. Full keyed run-plan composition and
caller-wait versus authoritative-run cancellation remain open.

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

| Owner                         | Outstanding implementation or proof                                                                                  |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Architecture index            | End-to-end source-of-truth conformance and complete coverage ledger                                                  |
| Foundation contracts          | Validated values, compatibility snapshots, versioning and deterministic primitives                                   |
| Project structure             | Missing owners, both dependency graphs, required project/test topology                                               |
| Composition and configuration | Full closed runnable graph, catalog publication/reload, keyed selection, scope ownership, readiness                  |
| Agent runtime                 | Explicit state transitions, waiter cancellation, recovery identity, settlement outcomes                              |
| Budgets                       | Replaceable ledger/profile/policy/event contracts, consumer integration, durable accounting and full conformance     |
| Messages and history          | Immutable/loss-aware values, non-elevation, correlation and shared round-trip conformance                            |
| Input and output              | Admission, durable promotion, lane routing, fan-out, final publication and channel contracts                         |
| Structured output             | Complete candidate extraction, validation, repair decisions and conversion conformance                               |
| Context                       | Instruction precedence, contributor trust/order, bounded assembly and request manifests                              |
| Context compaction            | Safe cuts, trustworthy activation evidence, cancellation, cursor/manifest reconciliation                             |
| Identity                      | Missing `AgentKit.Identity`; trusted ingress, tenant isolation and delegation lineage                                |
| Model and embedding providers | All advertised operation/capability mappings, endpoint/account bindings, terminal/error/usage semantics              |
| Tools                         | Authoritative terminal records, rejection projections, scheduling, retries and focused feature contracts             |
| Permissions and human control | Policy algebra, grants, approval persistence/replay, selectors, required audit and bounded infrastructure bootstrap  |
| Sessions                      | Lane operations, receipt-before-conflict replay, branch fencing, retention/export/import; missing SQLite backend     |
| Durable execution             | Missing runtime and explicit backend; journals, codecs, leases, checkpoints, evidence and recovery                   |
| Memory and retrieval          | Missing runtime/storage ownership; documents/vectors, retrieval provenance, tombstones and purge                     |
| Goals and delegation          | Durable goals/attempts/intents, joins, communication, parent occupancy and missing hosting worker                    |
| Hooks and extensions          | Typed point coverage, ordering, mutation validation, failure precedence and timeout quiescence                       |
| Observability                 | Complete safe signals, reusable assertions, required-sink separation and exporter implementation                     |
| MCP                           | Supported protocol eras, reflection, transports/lifecycle, capabilities and protected primitive adapters             |
| File system                   | Full dispositions/bounds/isolation semantics and missing deterministic in-memory backend                             |
| Network                       | DNS/send authority, connection reuse, redirects/retries, bounded streaming and egress evidence                       |
| Processes                     | Executable identity, sandbox enforcement, termination certainty, child effects and deterministic backend conformance |
| Artifacts                     | Reference-commit evidence, integrity/retention, finalize/abort races and fenced collection                           |
| Testing and evaluation        | Additional reusable contract suites, compatibility snapshots and missing evaluation owner                            |

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
