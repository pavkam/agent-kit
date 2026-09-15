# CodingAgent UX and capability work

CodingAgent must support sustained coding work with usable live feedback,
discoverable controls, safe permissions, and recoverable sessions. Reusable
missing behavior belongs in its owning AgentKit library. This ledger tracks the
full objective; a completed batch does not establish feature parity.

CodingAgent is a framework testing bed. Reusable behavior belongs in the owning
AgentKit library with tests; the example composes it. Tool request/result
presentation must be reusable, provider-neutral data, with renderer-specific
document and syntax-highlighting controls kept in the example. Transcript rows
must not use list-item selection; their textual content must support selection.
The interactive example selects SharpVision's bundled `turbo-vision` theme at
the console-application boundary, allowing every control family to inherit the
same catalog-owned palette and relief semantics.

## Verification approach

Drive the actual app in tmux, submit useful coding tasks, record failures,
assign bounded fixes to Sol agents, then repeat the same tasks against the
changed app. Use deterministic tests for ordering, cancellation, authorization,
persistence, and other behavior that a live model run cannot prove reliably.
Preserve the architecture and coding-harness profile as contract authority.

## Baseline: 2026-09-14

Source baseline: `49e1a0b`. Worktree was clean. A tmux session named
`codingagent-ux` ran the compiled CodingAgent at 120 columns by 40 rows against
an isolated temporary workspace containing a Python addition bug and a unit
test.

Submitted task: read both files, fix subtraction used as addition with `edit`,
run `python3 -m unittest -v`, and explain the tested result.

| ID      | Evidence                                                                                                                                                                       | Required change                                                                                                                                         | State         |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------- |
| UX-01   | Neither file read appeared before the edit approval. `SendAsync` projects events only after the loop returns.                                                                  | Expose incremental model and tool events through core contracts, then consume them in the UI.                                                           | In progress   |
| UX-02   | Only the footer spinner animates; the body contains no thinking or executing-tool indicator.                                                                                   | Show truthful in-body activity and correlated running tool cards.                                                                                       | In progress   |
| UX-03   | Startup exposes only Session and Help menus, with no permissions control.                                                                                                      | Discoverable session, agent, view, permissions, and help controls with actual selectable state.                                                         | In progress   |
| UX-04   | Edit approval shows escaped JSON (`new_text` contains `\\u002B`) without a readable change preview.                                                                            | Render path and before/after text, explicit choices, and decision state.                                                                                | In progress   |
| UX-05   | The prompt still says to ask the agent while it is read-only during approval.                                                                                                  | Explain current input behavior and offer visible cancellation.                                                                                          | In progress   |
| UX-06   | After approval, the app proceeds to command approval without showing the edit result or completed reads.                                                                       | Preserve live event order without replaying duplicate cards at completion.                                                                              | In progress   |
| UX-07   | Source inspection: `/new` menu can dispose the conversation while a turn is running; the runtime never disposes the built service provider.                                    | Coordinate reset/shutdown with active work and own the composition lifetime.                                                                            | In progress   |
| UX-08   | Test command exited 1; the transcript's failure card says only that the command exited 1. The model mentions Xcode developer-tool errors.                                      | Inspect retained command output, distinguish host/sandbox failures, and show useful bounded failure details.                                            | Open          |
| UX-09   | The final assistant response extends below the viewport, and the transcript is not keyboard-focusable.                                                                         | Verify scrolling, follow-latest, and end-of-response visibility at several terminal sizes.                                                              | Open          |
| UX-10   | PageDown has no effect. After resizing to 80x24 and submitting another read, usage advances and the prompt clears, but the viewport stays on the older cancellation entry.     | Fix keyboard scrolling and follow-latest for variable-height transcript rows; adapt the sidebar to narrow terminals.                                    | In progress   |
| CORE-02 | Retrying the test with the installed Homebrew Python path exits 126 with `Operation not permitted`. The macOS sandbox only exposes fixed system directories and the workspace. | Support explicitly configured, captured runtime/toolchain read roots in the process boundary without weakening workspace-write or network restrictions. | Open          |
| CORE-03 | The command tool always supplies an empty environment and defaults to a login shell. The live command reports `/etc/profile` access failure.                                   | Explicit non-secret environment projection and non-login execution for this example; no implicit ambient environment forwarding.                        | Open          |
| CORE-01 | Baseline README records repository glob/search exhausting traversal bounds; source prompt compensates by directing models away from broad searches.                            | Explicit exclusions that prune traversal under the filesystem contract, exposed by the tool schemas.                                                    | Investigating |

The baseline task changed the file but did not produce a passing test run. That
is a partial coding result, not a verified success.

Escape interrupted an approved 20-second sleep command and returned the app to
Ready. A follow-up read advanced usage, but the viewport did not reveal its
response at 80x24. Command and source evidence local to this audit is retained
under `/private/tmp/codingagent-evidence`; these temporary captures contain no
credentials and are not durable memory.

## Verified first implementation batches

The third fresh tmux process consumed live conversation events through the
owned-session wrapper. Both completed file reads appeared while command approval
was still pending. The command then passed with `PASS: addition returns 5`, and
the final assistant response scrolled into view at 120x40. Startup showed the
active Ask-for-changes permission mode. This verifies the actual example path,
including a wrapper-forwarding regression that component tests initially missed.

Core deterministic suites passed for live loop/conversation events,
cancellation, observer isolation, correlation, and final-event deduplication
(Loop 60 tests; Conversations 68 tests). The example's eight tests include live
forwarding before turn completion and composition disposal. The example analyzer
build passed with zero warnings or errors at this checkpoint.

Filesystem/tool exclusion support passed 3,846 relevant tests across
abstractions, both filesystem adapters, glob, and search. Excluded subtrees are
pruned before descent, while encountered excluded entries still consume
traversal bounds. Requests without exclusions preserve their previous security
fingerprints.

SharpVision 1.6.0-beta.1 was tested in an isolated reproduction project. Closed
sibling menus did not steal text-input arrow navigation, and appending rows then
calling `BringIntoView` revealed the final row in a basic variable-height list.
Those suspicions are not verified library bugs. Any verified SharpVision defect
will be filed in `pavkam/sharp-vision` with version and reproduction evidence.

The remaining checks include permission-menu operation, denial and cancellation
after the UI changes, narrow-terminal scrolling, and explicit toolchain access.
Source inspection also confirmed that `AgentKit.Session.Sqlite` does not yet
exist: a durable adapter is required before session resume can survive restart.

The fifth process verified selectable permission radios and prompt focus after a
menu action. Read-only mode denied a command before execution. Switching back to
Ask for changes and approving `python3 -m unittest -v` produced exit 0 and one
passing test with explicit Homebrew read access and PATH. The earlier command
card now says Awaiting approval while it waits, rather than claiming execution.

That denial exposed a provider bug: the model received empty result content
without its rejection outcome and falsely described the command as executed.
OpenAI-compatible translation now supplies a bounded, versioned status envelope
with exact outcome/uncertainty and ordered content. Runtime notices also remain
under user authority instead of being promoted to system messages. The provider
suite passed 150 tests, including denied/failed/cancelled outcomes, unknown
status, unresolved tool identity, literal mixed content, and
source/serialization bounds.

Reusable tool presentation now has library contracts, feature-owned formatters,
and exact captured conversation bindings; final review and live integration are
in progress. Generic owned-conversation wrappers were also extracted into the
Conversations library, with Open/List/live Send forwarding and disposal tests.
The Conversations suite now passes 82 tests, including versionless provider
calls and results presented through exact captured descriptors without rewriting
their original identity evidence. Read/Write formatter and tool suites pass 62
tests; Edit passes 18 and Command passes 31 after preserving replacement scope,
literal contents, write disposition, stream labels, and incomplete-output
evidence.

The eleventh fresh process verified a three-line bracketed paste remaining in
the composer without submission, followed by explicit Enter. Its write approval
showed the exact proposed Python contents and create-or-replace disposition. The
approved write succeeded, `python3 -m unittest test_mean -v` passed both average
and empty-input tests, and the assistant reported that result correctly. A prior
denied-write run explicitly reported the denial and waited for user direction.

Text selection is now live-verified. The transcript's generated `ListView` item
wrapper intercepted pointer input before selectable `Document` rows, so the app
now uses an intrinsically scrollable retained `Stack`; list-item selection is
structurally absent while each row remains a selectable semantic owner. A fresh
real SGR drag painted selected text in the exact CodingAgent layout, and tests
cover prose copy semantics. The exact isolated reproduction also verified a
SharpVision 1.6.0-beta.1 defect, filed as
[`sharp-vision#1187`](https://github.com/pavkam/sharp-vision/issues/1187) with a
runnable two-file gist. Long assistant prose now wraps in the same layout.

The permission audit found the documented approval broker missing from the
framework. The owning libraries now provide the store, broker, authenticated
responder authorization, audit checks, and security-authority integration. A
fresh v13 process used that path for an approved edit and independently approved
command; switching the same process to Read-only denied process execution before
invocation. An approval held past its deadline exposed misleading
submitted-state copy. The UI now shows the exact deadline, expires the wait, and
reports only that approval was submitted while the broker validates retained
evidence.

Write behavior now requires an explicit disposition. The tool exposes
create-only, replace-existing, create-or-replace, and append; both filesystem
implementations enforce replace-existing atomically and fail without creating a
missing target. The focused write and filesystem suites pass 41, 95, and 90
tests respectively.

The human-question feature is also composed through the shared AgentKit.IO
broker and Question tool. A fresh v14 process displayed two bounded typed
options, accepted one numbered answer, projected the selected result through the
reusable Question formatter, and continued to a separately authorized command.
The command's missing `pytest` executable remained an explicit exit-127 failure
with labelled stderr.

The new Session SQLite adapter persists the session directory, store state,
create and delete replay evidence, and tombstones in one explicitly selected
database. All 16 adapter tests pass, including dispose/reopen and fresh-codec
envelope coverage. CodingAgent composes this adapter with a stable per-workspace
agent identity. The default database lives under workspace-keyed platform
application data, outside the model-writable workspace; an absolute override may
select another target without merging workspace identities.

Session discovery and resume now use reusable Conversations APIs for bounded
list, open, paged history, and captured tool presentation. A real two-process
test created a user/assistant exchange, exited, listed the same session from a
fresh process, resumed it, hydrated both messages, and restored 2.4K reported
usage. That test also found a loop/codec schema contradiction: loop-authored
message entries used `1.0` while the durable codec promised `1`. The loop now
authors `1`; the codec rejects embedded or authored schema mismatches before
persistence. Existing malformed test databases continue to fail closed.

A live committed edit exposed one remaining raw-JSON formatter path. The Edit
formatter now consumes the tool's actual committed payload and renders bounded
status, replacement count, byte count, and visibility evidence; its 20 tests
pass.

Resource and Skill tools now own descriptor-bound presentation formatters as
well. Their actual list/read/activate results render bounded text or code while
omitting transport JSON, fingerprints, private backing paths, and unsafe failure
detail. Resource passes 19 tests and Skill passes 20. Automatic skill inventory
still needs a reusable `ISkillCatalogContextSource` adapter into
AgentKit.Context before the example can expose discoverable skills prior to
activation.

That adapter cannot be added honestly as a Skill-only bridge. The first context
milestone now defines the provider-neutral candidate kind, scope, cost,
freshness, diagnostics, `HistoryView`, repair evidence, and an atomic
definition/identity/ authorization/configuration evidence capture. The reduced
request has an additive evidence-aware constructor while retaining its
compatibility constructor. The authorization scope still has no conversation
coordinate, so that specific cross-check remains an explicit contract limit. The
second milestone now carries exact definition/configuration evidence through
additive run publications and requests, captures immutable session-prefix reads,
preserves conversation and branch provenance, and builds each turn's
`HistoryView` with fresh authorization. Session version and entry sequence are
tracked independently after multi-entry appends. Paged continuations accept only
exact snapshots previously issued by the adapter; invented
version/upper-sequence pairs, evicted evidence, and SQLite adapter restarts fail
with a typed result instead of being treated as captured provenance. The
remaining order is to add deterministic contributor registration, budgets,
manifests, and provider-neutral non-instruction projection, then register
Skill's bounded reference-data contributor from the same immutable catalog used
by its tool. Flattening inventory into a synthetic system/developer message
would lose provenance and is not an acceptable shortcut.

The final repository gate built the complete solution with zero warnings or
errors and passed all 8,254 solution tests; CodingAgent's separate suite passes
86 tests. An earlier full run exposed and fixed one optional-composition bug:
hosts without an approval store can validate unrelated permissions services,
while resolving the approval broker itself still fails explicitly until storage
is selected.

Help > Commands, the command palette's slash-command action, and `/help` now
open one catalog-backed TurboVision modal. Its selectable, vertically scrollable
`SharpVision.Document` renders grouped Markdown with exact syntax, effects, and
invocation guidance for every accepted command. The dialog was live-verified at
80x30 and 40x24, including Page Down navigation and Escape close.

The latest 80x24 audit reproduced `what is the source about`. Durable history
proved the loop continued through glob/read calls and committed a final answer
while the screen remained on an earlier glob row. Two application issues caused
that illusion: the transcript declared scrollbars without enabling `AutoScroll`,
and tool code used a nested scrollable `CodeView`. The transcript now owns
scrolling, presentation code uses natural-height `DocumentCodeBlock` nodes, and
exact follow-tail reaches the final assistant response. A follow-up prompt and
answer also remained visible at the tail. No additional SharpVision defect was
established.

The composer now grows with explicit newlines and visual wrapping. Its content
height is `max(1, min(5, floor(terminal width × 0.10)))`; overflow scrolls
inside the editor, and deleting or submitting text shrinks it to one row. Live
80x24 evidence covered one, three, and seven input lines, while 30x24 evidence
covered the proportional three-row cap and wrapped input. Multiline paste
remained unsubmitted. CodingAgent also loads SharpVision's bundled
`turbo-vision` theme through the console builder; the themed menu, adaptive
composer, Documents, and syntax blocks were verified together. Current key
routing is also live-verified: Enter submits the composer while Shift+Enter
inserts a retained newline.

The hand-built slash autocomplete popup has been replaced by SharpVision's
native `CommandPalette`. Pressing `/` or Ctrl+K opens a responsive TurboVision
surface with 19 searchable actions across sessions, transcript navigation, agent
control, permission modes, diagnostics, help, and application lifecycle. Rows
carry a title, description, truthful shortcut where one exists, category, and
current-state badge. Search, keyboard invocation, permission-mode execution,
Escape dismissal, and 80x24 plus 30x24 layout were live-verified.

Agent, model, status, tools, and workspace menu/palette actions now open native
TurboVision modal windows. Agent Configuration selects Terra, Sol, or Astra,
portable reasoning effort on a slider, the per-run tool-turn limit, and
permission mode while showing run/tool/sandbox state. Available Tools presents
all nine registered tools and the live safety policy in a selectable Markdown
document. Configure Workspace shows the fixed writable root, selectable
host-declared read-only toolchain roots, permission mode, network isolation, and
the session-store target. Saving either disposes the prior conversation and
composes a fresh one, preserving AgentKit's pinned run and session configuration
semantics. The same work added reusable `LlmReasoningEffort` to
`LlmRequestSettings` and OpenAI-compatible `reasoning_effort` translation with
contract tests. Both dialogs were live-verified at 80x30 and 34x24.

Help → Keyboard shortcuts, the palette action, and `/keys` now share a third
TurboVision modal. Its selectable `SharpVision.Document` renders an authored
Markdown reference grouped by composer, palette, transcript,
approvals/questions, and application navigation, with formatted key names and
precise behavior.

A second live race held that selection while a background turn advanced reported
usage from 15.9K to 22.7K and settled. The selected row stayed stable; later
rows now append immediately while replacement of the selected semantic source
waits until selection clears. Clearing then exposed an application
collection-mutation crash: the final selection callback rebuilt transcript
children inside the clear loop. Clearing now snapshots the `Document` rows
first; Escape clears selection, safely publishes deferred rows, and restores
prompt focus. A deterministic regression covers a callback that mutates the
original collection.

## Remaining capability audit

These requirements remain open until source and runtime evidence prove their
usable end-to-end behavior. Existing packages alone do not establish
integration.

| Capability                                              | Current evidence                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | Verification needed                                                                                                                                                        |
| ------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Live text, reasoning status, tools, usage, cancellation | Incremental loop/conversation events and owned-wrapper forwarding are implemented and live-verified.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | Final combined regressions for terminal states, retry behavior, and no duplicate effects/cards.                                                                            |
| Permissions and approvals                               | Shared approval broker, exact responder identity, expiry, audit, and normalized file/process policies are composed and live-verified.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              | Persistent approval storage and reconnect behavior where a durable host requires them.                                                                                     |
| Code inspection, edits, patches, commands               | Nine model-facing tools are composed; patch/list packages also exist. A Terra run used the sandboxed command tool and Homebrew `rg` to verify the corrected repository URL with exit code 0.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       | Readable multi-file diffs and broader project test runs from the agent.                                                                                                    |
| Session persistence and navigation                      | Session SQLite is composed outside the writable workspace; `/sessions` and `/resume` survive a real process restart, hydrate messages and tool presentations, and restore reported usage.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | Session naming, branch/navigation where supported, and safe recovery of active or interrupted runs.                                                                        |
| Model and execution configuration                       | Native Agent Configuration selects Terra/Sol/Astra, portable reasoning effort, 4–24 tool turns, and permissions; saving recomposes a fresh runtime.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                | Provider-discovered model catalogs, model-specific effort/limit validation, and context-limit display.                                                                     |
| Context and project resources                           | Resource and Skill own safe reusable presenters. Context milestones now supply candidate/history evidence and propagate exact definition, configuration, authorization, conversation, branch, version, and sequence evidence through loop assembly; the packages are not yet composed into CodingAgent.                                                                                                                                                                                                                                                                                                                                                                                                                                                                            | Implement ordered contributors, budgets, manifests, provider projection, project instructions, bounded attachments, compaction, and reusable skill-inventory contribution. |
| User questions and task planning                        | Todo plus authenticated, deadline-aware Question broker/tool/channel are composed; numbered answers are live-verified.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             | Durable pending-question recovery and durable task status across restart.                                                                                                  |
| Delegation and background work                          | Task/goal packages exist but are not composed.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     | Bounded child tasks, progress/results, cancellation, safe permissions and ownership.                                                                                       |
| Extensibility                                           | MCP and other tool packages exist but are not composed.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            | Configured server/tool discovery, capability display, failures, lifecycle and trust controls.                                                                              |
| Terminal usability                                      | TurboVision theme, native 19-action command palette, two responsive configuration dialogs, direct live model/reasoning radio submenus, a spaced-pipe status bar for run/permission/usage/model-effort/workspace context, role-neutral user/assistant prose without redundant headings, selectable Markdown dialogs for available tools, keyboard shortcuts, and the catalog-backed slash-command reference, Enter-to-send and Shift+Enter newline routing, adaptive one-to-five-row composer, multiline paste/send, menus, wrapped prose, 80x24 responsive collapse, transcript-owned paging/follow, natural-height tool output, and retained-row text selection are live-verified; later rows remain visible while selected sources stay retained; list-item selection is absent. | Additional terminal protocol profiles.                                                                                                                                     |
| Repeatable evidence                                     | Initial live calculator run and source audit recorded above.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       | Repeated tmux runs after each batch, focused tests, and repository gates for final completion.                                                                             |

The full CodingAgent objective remains active.

## Menu bar, dialogs, and permissions consolidation

The menu bar was a `Menu` inside a padded, bottom-bordered `Dock`, so its inset
cells and the rule beneath it took the desktop color while the rows sat on the
theme's Bar plane; the inset is now the menu's own padding and the strip itself
is the boundary, edge to edge on one plane. The five configuration and reference
windows were plain `Window`s shown through `ShowModal` with hand-rolled button
rows and no separator; they are now `Dialog<TResult>` subclasses over one
`CodingAgentDialog<TResult>` base that composes body, separator, and a centered
action bar, built fresh per presentation and disposed on close. A mis-escaped
`"Save & start fresh session"` button had been rendering as `Save  start` with
Alt+Space as its access key; buttons now carry real `&Save`, `&Cancel`, and
`&Close` mnemonics. Permission mode had radios in the menu and in both
configuration dialogs plus three different wordings; it now lives in the
`Permissions` menu (with the palette and `/permissions [ask|readonly|auto]`
running the same actions) and every label reads from `PermissionModeCatalog`.
Duplicate menu aliases (`Status`, `Workspace`, a second `Agent configuration`, a
second `Clear transcript`) and clashing Alt keys in the Agent menu are gone,
menu items are found by label instead of index, and the advertised `Ctrl+Q`
finally has a bound gesture. SharpVision 1.6.0-beta.2 supplies the menu fixes
this relies on: drop-downs paint their own plane, keyboard navigation no longer
opens submenus on its own, and focus returns to the composer when a menu closes.
