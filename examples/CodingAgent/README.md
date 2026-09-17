# CodingAgent

A real, working terminal coding agent built on AgentKit: OpenAI for the model,
real sandboxed file and process tools, and
[SharpVision](https://github.com/pavkam/sharp-vision) for the whole terminal UI.

This example exists to prove out — and stress — both libraries end to end. It
composes AgentKit's real production components (`DefaultAgentLoop`,
`DefaultSessionCoordinator`, `SecurityAuthority`, the OpenAI provider, and eight
tool packages, including `AgentKit.Tools.Plan`'s `todo` tool and
`AgentKit.Tools.Question`'s authenticated human-question tool) directly, and
renders the conversation with SharpVision's `Document`/Markdown control, a menu
bar, a command palette, a session/usage/plan sidebar, a status bar, and a
spinner. The interactive host loads SharpVision's bundled `turbo-vision` theme
at startup (override it with `CODING_AGENT_THEME`, or switch live from View >
Theme), so its published palette and relief flow through the complete control
tree without application-owned color copies. Native modal dialogs configure the
workspace and agent and present formatted help without dumping either into the
transcript. Every tool call in this example touches your real filesystem and a
real sandboxed subprocess — there is no mock mode.

## Run it

Copy [`.env.example`](.env.example) to `.env.local` (already gitignored) and
fill in your own key, so you don't have to export it in every shell:

```bash
cp examples/CodingAgent/.env.example examples/CodingAgent/.env.local
# edit .env.local and set OPENAI_API_KEY=sk-...
dotnet run --project examples/CodingAgent/CodingAgent.csproj -- /path/to/a/workspace
```

`.env.local` is loaded once at startup and never overrides a variable already
exported in your shell, so `export OPENAI_API_KEY=sk-...` still works exactly as
before if you prefer that. The workspace argument is optional; it defaults to
the current directory. The default model is `gpt-5.6-terra`; set
`CODING_AGENT_MODEL` to `gpt-5.6-sol` or `gpt-6-astra` for the initial
selection, or choose Terra, Sol, or Astra in Agent Configuration. Reasoning
defaults to **Off** because OpenAI Chat Completions rejects Terra/Sol requests
that combine function tools with another reasoning effort; the AgentKit setting
maps explicitly to `reasoning_effort: none`. Type a message; Enter sends it,
Shift+Enter adds a new line, and multiline paste stays in the composer. Press
`/` or Ctrl+K to open the native TurboVision command palette. Its 19 searchable
actions cover sessions, transcript navigation, agent control, permission modes,
runtime information, help, and application actions. The agent can read, write,
edit, glob, search, run shell commands (sandboxed to the workspace, no network),
maintain a typed plan through `plan` or its `todo` compatibility alias, and ask
a bounded multiple-choice question against that exact session.

Sessions are stored durably beneath the platform-local application-data root in
a workspace-keyed directory, outside the model-writable workspace. The same
workspace can therefore reopen its conversation history after the process exits.
Set `CODING_AGENT_SESSION_DB` to an absolute path to place the SQLite file
elsewhere. CodingAgent creates the selected parent directory explicitly; the
AgentKit SQLite adapter never creates parent directories. A stable agent
identity is derived from the normalized workspace, so even a shared database
override keeps discovery and reopening isolated by workspace. The path contains
no credentials and should not point at a shared network filesystem.

Sandboxed commands receive no ambient environment. Set
`CODING_AGENT_TOOLCHAIN_ROOTS` to a platform-PATH-separated list of absolute
read-only roots and `CODING_AGENT_COMMAND_PATH` to the exact PATH projected into
commands. For Homebrew Python on Apple Silicon, use `/opt/homebrew` for the
former and `/opt/homebrew/opt/python@3.14/bin:/opt/homebrew/bin:/usr/bin:/bin`
for the latter. You can also add read-only folders without restarting: Session >
Configure workspace (or `/workspace`) lists the host-declared roots and lets you
type any absolute folder (`~` expands), validates that it exists and sits
outside the workspace, and starts a fresh session with the new selection.

Set `CODING_AGENT_THEME` to any bundled SharpVision theme slug (for example
`nord`, `dracula`, `tokyo-night`, `catppuccin-mocha`, `gruvbox-dark`,
`solarized-light`) to start with that palette instead of `turbo-vision`; an
unknown slug falls back to the default. View > Theme switches at run time.

Type `/` for a live list of slash commands (filtered as you keep typing):

| Command                              | Does                                                        |
| ------------------------------------ | ----------------------------------------------------------- |
| `/help`                              | Open the formatted slash-command reference.                 |
| `/clear`                             | Clear the transcript; the agent's session/memory continues. |
| `/new`                               | Start a brand-new session, discarding conversation history. |
| `/sessions`                          | List recent durable sessions for this workspace.            |
| `/resume <id>`                       | Reopen a recent session and hydrate its stored transcript.  |
| `/model`                             | Open Agent Configuration at the model selector.             |
| `/workspace`                         | Open Configure Workspace for read-only toolchain roots.     |
| `/status`                            | Open Agent Configuration with live run status.              |
| `/tools`                             | Open the selectable tool and sandbox reference.             |
| `/keys`                              | Open the formatted keyboard-shortcuts reference.            |
| `/permissions [ask\|readonly\|auto]` | Show or switch the live permission mode.                    |
| `/quit`                              | Exit CodingAgent.                                           |

A headless mode skips the UI entirely, useful for scripting or diagnosing an
AgentKit-side problem in isolation from SharpVision:

```bash
dotnet run --project examples/CodingAgent/CodingAgent.csproj -- --smoke-test /path/to/workspace "your prompt"
```

## Working like a real coding agent

This isn't a demo that only ever reads files — it's meant to feel like sitting
in front of Codex or Claude Code:

- **A real menu bar** (`Session` / `View` / `Agent` / `Permissions` / `Help`)
  sits above the transcript, reachable by Alt+mnemonic, duplicating every slash
  command as a discoverable menu item — New session, Clear transcript,
  Workspace/Model info, Quit, and a full command list.
- **Configuration uses real dialogs.** Agent Configuration and Configure
  Workspace derive from SharpVision's `Dialog<TResult>`, so they get the
  framework's modal presentation, the separator above a centered `&Save` /
  `&Cancel` action bar, typed completion, and disposal on close. Agent
  Configuration edits the model, reasoning effort, and tool-turn limit;
  Configure Workspace shows the required writable root, selectable host-declared
  read-only roots, sandbox/network posture, and session store. Saving either
  starts a fresh conversation runtime so admitted sessions retain exact
  configuration evidence.
- **Typing `/` opens a command palette**, not just an inline hint: a bordered
  popup lists every matching command with its description, filtered live as you
  keep typing. Ctrl+P/Ctrl+N move the highlighted row, Tab or Enter accepts it
  (or, if you've already typed the full command yourself, Enter just runs it —
  no need to accept your own typing first), and Escape closes it without
  touching your text.
- **Permission modes live in exactly one place.** The `Permissions` menu owns
  the three radio rows; the command palette and `/permissions` run the same
  actions, and the status bar shows the current mode. Every label comes from one
  `PermissionModeCatalog`, and changing the mode never restarts the session. The
  default asks before every write, edit, or shell command. Read-only rejects all
  three, while auto-approve workspace edits allows write/edit calls but
  continues to ask before commands. The transcript shows a readable edit diff
  and waits for `y`/Enter (approve) or `n`/Escape (deny) before the call runs. A
  denial is reported back to the model as a real tool result — the agent is told
  the action did not happen and is instructed never to claim otherwise.
- **Human questions pause the active turn without fabricating authority.** The
  shared question broker consumes the exact publication grant before the TUI
  sees a grant-free prompt. Choose one of 2–10 numbered options, add free text
  when the question permits it, and press Enter. Shift+Enter adds a line when
  free text is permitted. Escape cancels the active turn; an expired deadline
  cannot become an answer.
- **Escape cancels a running turn.** While the agent is thinking or a tool is
  executing, press Escape to interrupt it immediately; the status bar reminds
  you of this (`Thinking... (Esc to cancel)`). The turn ends with a "Cancelled"
  entry in the transcript, and the session is left in a fully valid state — you
  can keep chatting (or cancel again) right away.
- **Ctrl+P/Ctrl+N recall your last inputs**, exactly like a shell history,
  including slash commands, while arrow keys remain available for caret
  movement.
- **Tool calls and results render live as correlated accent-bordered cards**,
  with an in-body spinner while each call is active. Assistant text arrives
  incrementally, and reasoning deltas use their own temporary thinking card when
  the provider supplies them. A generic thinking card appears immediately even
  when the selected model emits no reasoning metadata. A colored left rule (blue
  for the assistant, dim for a tool call, green/red for a tool result's success
  or failure, red for errors) makes a long transcript scannable at a glance
  instead of a wall of undifferentiated Markdown.
- **Tool previews and results use each tool package's reusable presentation**,
  rendered as literal text, code, or diffs instead of exposing wire JSON.
  Formatter-bounded code and file contents use line-preserving native document
  code blocks, so a tool card expands to its natural height and only the
  transcript scrolls. Markdown remains reserved for assistant and user prose,
  where CommonMark formatting is actually wanted.
- **A right-hand sidebar tracks live session context**: a running token/cost
  total (from the real `ConversationUsageEvent` the model provider reports — see
  the gap below) and a live todo checklist (☐ pending, ◐ in progress, ☑
  completed, ✗ blocked), parsed straight from the `todo` tool's own results as
  the agent works through a multi-step task. The system prompt tells the model
  to use the todo tool for anything beyond a couple of tool calls.
- **The status bar carries live context**: a compact tokens/cost summary, the
  current model id, and the workspace's directory name sit right-aligned,
  separated from the busy/ready indicator so you never lose track of what a
  session has cost or which agent/workspace it belongs to.

### Remaining UI gaps

The composer supports multiline editing and safe multiline paste. The sidebar
collapses on narrow terminals, and Page Up/Page Down plus Follow latest provide
explicit transcript navigation. SQLite preserves conversation sessions across
process restarts; `/sessions` discovers a bounded recent page and `/resume`
opens one while the agent is idle and hydrates its bounded stored history. The
application selects SharpVision's bundled Turbo Vision theme at startup and
paints the working area on the theme's application-window plane, so Turbo
Vision's dithered desktop shows only behind dialogs; menus, the responsive
paired-line command palette, documents, approvals, and the composer inherit
whichever theme is active.

## What this proves

Composing a single working conversational turn — session creation, a user
message, a multi-turn tool-calling loop, and a real provider round trip —
originally needed far more than the four packages `AgentKit.Loop`'s own
registration doc lists (`ISessionCoordinator`, `IContextAssembler`,
`IToolInvoker`, one `ILlmModel`). Building this example the first time surfaced
eight distinct composition gaps, all documented below. **Every one of them has
since been fixed at the library level**, and `AgentRuntime.cs` now composes the
fixes directly instead of working around the gaps by hand: see
[the git history of this file](https://github.com/pavkam/agent-kit/commits/main/examples/CodingAgent/AgentRuntime.cs)
for the before/after, or read `AgentKit.Conversations`, `AgentKit.Permissions`,
and `AgentKit.Tools`'s READMEs for the new APIs themselves.

## Gaps found in AgentKit while building this — and how each was fixed

None of these were exotic: every one blocked the very first message of any
application composing a real conversational turn, and this example was (as far
as a repository-wide search showed) the first thing in this checkout to ever
exercise this exact path.

1. **No facade method admitted a user message into a run.** `AgentRunOptions`
   carries a session/branch/identity and bounded overrides, but no message, and
   `AgentEngine` deliberately never exposes its container. **Fixed** by the new
   `AgentKit.Conversations` package: `AddConversationSession` +
   `IConversationSession.SendAsync` compose session creation, message admission,
   and one full agent-loop run behind a single call, for exactly the "one
   in-process conversation with one composed agent" case this example needs.
   `AgentRuntime.cs` no longer hand-rolls
   `ISessionCoordinator`/`ISecurityProfileSelector` calls at all.
2. **No first-party `ISecurityPolicy` allowed process execution**, and
3. **no first-party `ISecurityPolicy` allowed session state either** — both
   **fixed** by the new `AllowAllSecurityPolicy` in `AgentKit.Permissions`,
   registered with `AddAllowAllSecurityPolicy()`. It's a deliberately blunt,
   local/single-tenant-only instrument (see its own remarks); this example's two
   hand-written policy files (`AllowWorkspaceProcessExecutionPolicy.cs`,
   `AllowLocalSessionStatePolicy.cs`) are gone because the library now ships the
   equivalent.
4. **A silent authorization trap:** `SecurityAuthority` denied every request
   with `"security.captured_context_mismatch"` unless
   `AgentPermissionOptions.PolicySnapshot` was explicitly configured to the
   exact same `SecurityPolicySnapshotReference` published for that profile — the
   default was `null`, so this failed for every composition that didn't know to
   set it, and getting it right required building a provider once to grab the
   constructed `ISecurityAuthority` before the keyed binding and publication
   could even be registered. **Fixed** by `AddStandaloneSecurityProfile` in
   `AgentKit.Permissions`: it derives the snapshot, wires
   `AgentPermissionOptions`, and registers the profile publication and a _lazy_
   keyed authority binding (a new
   `AddSecurityAuthority(ComponentKey<ISecurityAuthority>)` overload that
   resolves the container's own authority the first time it's needed) — no
   intermediate provider build required. `AgentRuntime.cs`'s ~90-line, two-build
   security setup is now six lines.
5. **Audit delivery was `Required` by default with no sink registered.**
   Unchanged by design (it's the correct fail-closed default), but
   `AddStandaloneSecurityProfile`'s `configurePermissions` delegate makes opting
   into `SecurityAuditDelivery.BestEffort` for a local composition a one-line,
   discoverable choice instead of a separate manual step.
6. **`AllowListToolAuthorizer` started with an empty allow-list and there was no
   way to populate it from the tools actually registered.** **Fixed** by
   `AgentToolsOptions.AllowAllRegisteredTools`: setting it to `true` grants
   every catalog-registered tool at once. `AgentRuntime.cs` no longer probe-
   builds a temporary provider just to collect `ToolId`s.
7. **The Command tool's required sandbox profile id wasn't discoverable without
   reading source.** Still just one supported profile
   (`PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile`), but the runtime
   failure now lists every registered profile id instead of a bare "not
   registered" message, so a typo or a genuinely unregistered profile is
   diagnosable without reading `OperatingSystemProcessRunner` source.
8. **No helper converted a tool's `ToolDescriptor` into the `LlmToolDefinition`
   a model request needs.** **Fixed** by
   `ToLlmToolDefinition`/`ToLlmToolDefinitions` in `AgentKit.Tools`.
   `AgentRuntime.cs` populates its tool list with
   `services.AddOptions<ConversationSessionOptions>().Configure<IEnumerable<ITool>>(...)`
   — no probe-build needed for this either, since `Configure<TDep>` resolves its
   dependency lazily when the options are first materialized.

The net effect: `AgentRuntime.cs` shrank from roughly 490 lines to about 185,
and the ~90 lines of security/tool-allow-list boilerplate that used to need two
separate `BuildServiceProvider()` calls are gone entirely — one build, at the
end, is all this example needs now.

Adding Escape-to-cancel (below) surfaced two more, more serious, gaps — serious
enough that a single cancelled command silently broke every future turn in the
session:

1. **`DefaultConversationSession.SendAsync` ignored `AgentRunOutcome`
   entirely.** Any outcome other than `AgentRunCompleted` — a turn limit, a
   cancelled run, a context-preparation failure, anything — was reported back as
   `Succeeded = true` with zero events, because `SendCoreAsync` never inspected
   which outcome variant it got. This turned every real failure into a silent,
   empty "success," which is exactly how the bug below stayed invisible until
   traced by hand. **Fixed:** `SendCoreAsync` now checks for `AgentRunCompleted`
   explicitly and otherwise reports `Succeeded = false` with a human-readable
   description of the actual outcome (turn limit reached, model selection
   failed, context preparation failed, cancelled, etc.), from a new exhaustive
   `DescribeIncompleteOutcome` switch covering every outcome type.
2. **A tool call cancelled mid-batch permanently corrupted the session.**
   `DefaultAgentLoop.InvokeToolsAsync` had no cancellation handling around
   `_toolInvoker.InvokeAsync`: cancelling while a tool was running either threw
   straight out of the loop (leaving the just-committed assistant message's tool
   call with no matching result) or — for `OperatingSystemProcessRunner`, which
   absorbs cancellation internally and returns a normal, non-throwing
   "cancelled" result — completed the batch but still hit this same broken state
   one layer up. Either way, every later turn in that session then failed
   context assembly with
   `ContextPreparationFailure { Kind = BrokenToolCallCausality }`, because the
   assembler requires every tool call to have exactly one matching terminal
   result. **Fixed:** `InvokeToolsAsync` now catches
   `OperationCanceledException` around each invocation and, once cancellation is
   observed (thrown or silently absorbed), synthesizes an `Interrupted` result
   for that call and every call still remaining in the batch, then
   unconditionally appends the turn with `CancellationToken.None` — every tool
   call always gets a matching result before cancellation is allowed to
   propagate.

Both fixes have regression tests (`DefaultConversationSessionTests`,
`DefaultAgentLoopTests`) covering the outcome-reporting fix and both
cancellation shapes (thrown and silently absorbed).

Building the sidebar (tokens/cost and a live todo list) surfaced two more gaps,
one a missing feature and one a real correctness bug that only a session-state
tool like `todo` could trigger:

1. **A model response's real usage evidence (input/output tokens, estimated
   cost) was computed by the provider and attached to every committed
   `AssistantMessage`, but `AgentKit.Conversations` never projected it into
   anything a caller could read** — `DefaultConversationSession.ProjectEvents`
   iterated an assistant message's content parts but never looked at its
   `Response.Usage`. A UI that wants to show running cost, like this example's
   sidebar and status bar, had no supported way to get it. **Fixed** by a new
   `ConversationUsageEvent(ModelUsage Usage)` case on the `ConversationEvent`
   hierarchy, raised right after a turn's other events whenever
   `ModelUsage.ReportState` isn't `NotReported` — a provider or model that never
   reports usage simply never raises it.
2. **A tool that commits its own session entries mid-turn permanently broke that
   turn**, and it's not exotic: the `AgentKit.Tools.Plan` package's `todo` tool
   does exactly this by design (`SessionPlanStateStore` persists every plan
   revision as a session entry through the same `ISessionCoordinator` the turn
   itself is using). `DefaultAgentLoop` tracks the branch's expected version
   once per turn in a local `currentVersion` variable and only advances it after
   its _own_ appends; it had no way to notice that the `todo` tool's append —
   running independently, in the middle of the same turn's tool batch — had
   already moved the real branch version forward. The turn's next append (the
   tool result) then failed with `SessionAppendConflict`, and — because of the
   bug above, before it was fixed — that failure surfaced first as a silent
   phantom "success" and only later as `ContextPreparationFailure`. With that
   bug fixed, it instead surfaced honestly as `AgentRunSessionOperationFailed`,
   but the run still never completed: asking this example to combine two files
   "using the todo tool to track your steps" reliably failed with _"the session
   branch advanced from the expected version 2 to 3 before the append
   committed"_ the moment the `todo` tool ran between two of the loop's own
   appends. `SessionAppendConflict`'s own remarks are explicit that this is
   deliberate — "the store never rebases the request against the newer version
   or silently retries; the caller decides whether to reload and reattempt with
   a fresh expected version" — but `DefaultAgentLoop` never did. **Fixed:**
   `AppendWithDiagnosticsAsync` now retries a conflicting append (bounded to 5
   attempts) by rebasing both the request's `ExpectedVersion` and every entry's
   own `Sequence` onto the conflict's reported `ActualVersion` before
   resubmitting — rebasing only the version and not the entries' sequence
   numbers looked like a fix on the first pass but just traded one error for
   another (a sequence-continuity rejection), since each entry's sequence had
   been computed from the stale version at build time. Both call sites (the
   assistant-message append and the tool-result append) share this one retry
   path. Two regression tests (`DefaultAgentLoopTests`) simulate exactly this
   interleaving — a conflicting append followed by a successful retry at the
   corrected version — for both the assistant-message and tool-result append;
   the two pre-existing tests asserting a _persistent_ conflict still fails
   outright continue to pass unchanged.

Two more things surfaced only by actually driving this example against a real,
substantial repository (this one) instead of a small scratch directory — the
kind of gap that only shows up once you stop testing against a toy workspace:

1. **`glob` and `search` patterns provide zero traversal pruning of their own —
   only `base_path` does, and it is easy to assume otherwise.** Both tools' own
   descriptions already say they "never follow symlinks or consult ambient
   ignore files," but that undersells the practical consequence:
   `TraverseGlobDirectory`/`TraverseSearchDirectoryAsync` walk every entry under
   `base_path` (the workspace root, by default) unconditionally, and only check
   the entry's full relative path against `pattern`/`path_pattern` _after_
   visiting it. A literal directory prefix embedded in the pattern itself, like
   `"src/AgentKit.Loop/**/*.cs"`, reads as if it scopes the walk the way a shell
   glob would — it does not; the walk still covers the entire tree, `bin/`,
   `obj/`, `.git`-adjacent build output, and all. Pointing this example at this
   very repository and asking it to "find a bug" reproduced the failure
   directly: the model's very first call, a plain `**/*` glob at depth 3,
   immediately failed with "The glob retained-result limit was exceeded," and a
   narrower-looking pattern scoped to one small project still failed with "The
   glob visited-entry limit was exceeded" — because `src/AgentKit.Loop`'s own
   accumulated `bin/`/`obj/` output (from this session's own repeated builds)
   was still being walked in full before any filtering happened. This is not a
   library bug — `base_path` exists precisely to provide the traversal root a
   caller needs — but it is a sharp edge a model (or a person) reaches for the
   wrong tool to solve on the first try almost every time. **Addressed** in
   `AgentRuntime.cs`'s system prompt: it now states plainly that
   `pattern`/`path_pattern` only filters after the fact, that `base_path` is the
   only argument that actually limits what gets walked, and to glob a shallow
   `base_path` first to discover layout before ever recursing into a specific
   project directory.
2. **Multi-line tool output rendered as Markdown prose silently loses its own
   line breaks.** CommonMark joins adjacent non-blank lines into one paragraph
   unless every line ends with a hard break; feeding a tool result — a
   `read_file` result being the clearest case — through `Document` as plain text
   therefore collapsed an entire source file into one visually continuous line,
   which is exactly as unreadable as it sounds and was not caught until this
   pass actually read a real file back in the running app instead of only
   checking that a card rendered at all. **Fixed** by no longer routing tool
   calls, tool results, or file content through `Document`/Markdown at all:
   every one of them now renders through `CodeView` instead (see below), which
   preserves line structure unconditionally regardless of content and adds real
   syntax color on top when the extension or payload type is one the bundled
   catalog covers. `Document`/Markdown is now reserved for actual prose — the
   assistant's and the user's own messages — where CommonMark's own formatting
   is genuinely wanted.

## Rough edges found in SharpVision

- **The bundled `SharpVision.SyntaxHighlighting` catalog in this build is a
  curated ~160-grammar subset, not full mainstream coverage** — it has C#,
  TypeScript (and TSX/JSX), Rust, Swift, PowerShell, ANSI C89, CSV/TSV, JSON5 (a
  strict superset of JSON, close enough to highlight it correctly), and a long
  tail of genuinely niche formats (SELinux contexts, AHDL, IATA SSIM), but no
  plain JSON, Python, Ruby, Go, SQL, YAML, Markdown, or HTML grammar at all.
  `CodeView.Language` throws `KeyNotFoundException` for a name the catalog
  doesn't have, so guessing is not an option:
  [`SourceLanguage.cs`](SourceLanguage.cs) maps only extensions verified present
  in this build's own `Resources/syntax.manifest.json` and returns `null`
  (plain, unhighlighted, still line-preserving `CodeView` text) for everything
  else, rather than risk a crash on a `.py` or `.go` file.
- **No implicit initial focus.** A freshly attached `Screen` has no focused
  control until `OnStarted` explicitly calls `application.Focus.Focus(...)`.
  This is documented (`docs/concepts/screen.md`'s own example does exactly
  this), but it's easy to miss: the app renders correctly and looks interactive,
  then silently swallows every keystroke.
- **GFM emoji shortcodes (`:gear:`, `:white_check_mark:`) are not part of the
  supported Markdown dialect** and render as literal text rather than a glyph or
  being stripped — expected, since only baseline CommonMark is documented as
  supported, but worth knowing before reaching for them in a transcript like
  this one's.
- **A `ListView` defaults to `IsFocusable = true`/`IsTabStop = true`, so an
  externally driven display list can silently steal initial focus.** The
  transcript now uses retained document rows in a scrolling `Stack`, while the
  native `CommandPalette` deliberately owns focus and result navigation only
  while it is open.
- **`Menu`'s own default arrow-key handling (`Menu.OnEvent`) claims Up, Down,
  Left, and Right unconditionally, for any `Menu` anywhere in the attached tree,
  regardless of focus.** Unlike `ListView.OnKeyRouted` — which checks
  `eventArgs.OriginalSource` and ignores a stroke that didn't originate from one
  of its own rows — `Menu.OnEvent` has no equivalent guard. The moment a `Menu`
  is attached, arrow-key presses on a completely unrelated, correctly focused
  sibling (this example's prompt) stop arriving at that sibling's `KeyDown`
  event at all, whether or not the `Menu` itself is a tab stop or focusable.
  This was confirmed with a from-scratch, three-control reproduction built
  entirely outside this example: a bare `TextInput` next to a `Text` status
  label correctly reports every arrow key on its own; adding nothing but a
  `Menu` next to them — with `IsTabStop = false` and `IsFocusable = false` on it
  — silently and permanently stops all four arrow keys from ever reaching the
  `TextInput`'s `KeyDown` again. **Worked around** for composer history with
  Ctrl+P/Ctrl+N (the classic Emacs/readline chords). SharpVision's native
  command palette owns its popup navigation session early enough for Up/Down to
  select results correctly; that path is live-verified in the composed app.
- Everything else not covered above worked exactly as documented on the first
  try: dispatcher threading (an `async void` event handler's `await`
  continuation correctly resumes on the UI thread with no manual
  `Post`/`InvokeAsync`), `Document` reload-per-turn (no flicker even at several
  kilobytes of accumulated markdown), live terminal resize (reflow, scrollbar
  appearance, and docked status/prompt bars all stayed correct), and
  read-only-during-busy input blocking (a second Enter while a turn is in flight
  is silently dropped, never queued or crashed).
- Retained `Document` rows inside the transcript's scrolling `Stack` preserve
  natural-height prose and code blocks, text selection, and one outer
  follow-tail owner without nested scrolling.

## Files

- [`Program.cs`](Program.cs) — entry point; `--smoke-test` selects the headless
  path.
- [`AgentRuntime.cs`](AgentRuntime.cs) — composes the agent: security, session
  store, tools, provider, and the `AgentKit.Conversations` session.
- [`ChatScreen.cs`](ChatScreen.cs) — the SharpVision UI: a menu bar, a
  selectable document transcript opened by a welcome card, SharpVision native
  command palette, a session/usage/plan sidebar, a status bar with run,
  permission, usage, model/effort, and workspace context, per-call tool approval
  prompts, direct model/reasoning radio menus, a live theme menu, role-neutral
  user/assistant transcript prose, Escape-to-cancel, and Ctrl+P/Ctrl+N history.
- [`CommandPaletteItem.cs`](CommandPaletteItem.cs) — searchable application
  action metadata rendered by the native palette.
- [`CodingAgentDialog.cs`](CodingAgentDialog.cs) — the shared dialog base: one
  centered modal with titled sections, wrapped notes, and the framework action
  bar.
- [`AgentConfigurationDialog.cs`](AgentConfigurationDialog.cs) — model picker
  with description, reasoning-effort radio group, and a turn-limit slider with a
  live readout and labeled endpoints.
- [`WorkspaceConfigurationDialog.cs`](WorkspaceConfigurationDialog.cs) — the
  workspace card, the read-only folder list with an add-folder entry, and the
  sandbox summary.
- [`ToolchainRootPolicy.cs`](ToolchainRootPolicy.cs) and
  [`ToolchainRootValidation.cs`](ToolchainRootValidation.cs) — the pure
  validation behind that entry: absolute, existing, outside the workspace, not a
  duplicate, `~` expanded.
- [`MarkdownReferenceDialog.cs`](MarkdownReferenceDialog.cs) — the selectable,
  scrollable Markdown dialog behind Help, the palette, `/keys`, `/help`, and
  `/tools`; [`KeyboardShortcutsReference.cs`](KeyboardShortcutsReference.cs),
  [`CommandReference.cs`](CommandReference.cs), and
  [`ToolReference.cs`](ToolReference.cs) author its content.
- [`CodingAgentTheme.cs`](CodingAgentTheme.cs) — resolves the startup theme from
  `CODING_AGENT_THEME` and lists the bundled slugs the View menu offers.
- [`PermissionMode.cs`](PermissionMode.cs),
  [`PermissionModeCatalog.cs`](PermissionModeCatalog.cs), and
  [`PermissionModeController.cs`](PermissionModeController.cs) — the three
  approval modes, their single source of user-facing strings, and the live
  holder the security policy reads.
- [`CodingAgentHostEnvironment.cs`](CodingAgentHostEnvironment.cs) — reads the
  host-declared toolchain roots and command PATH.
- [`CodingAgentConfiguration.cs`](CodingAgentConfiguration.cs) — the immutable
  model, reasoning, turn, and read-only-root choices captured by the runtime.
- [`ChatEntry.cs`](ChatEntry.cs) — the transcript's per-message view model.
- [`SlashCommand.cs`](SlashCommand.cs) and
  [`SlashCommands.cs`](SlashCommands.cs) — command metadata accepted by direct
  composer submission and projected into formatted help without a second
  documentation list.
- [`SessionUsage.cs`](SessionUsage.cs) — accumulates the running token/cost
  totals from `ConversationUsageEvent`, for the sidebar and status bar.
- [`TodoItem.cs`](TodoItem.cs) — parses the `todo`/`plan` tool's own result JSON
  into the sidebar's live checklist, without touching `IPlanStateStore`'s
  protected read path.
- [`SourceLanguage.cs`](SourceLanguage.cs) — maps a file path to a verified
  `CodeView` catalog language name, and pretty-prints JSON tool payloads for
  display.
- [`CodingAgentSecurityPolicy.cs`](CodingAgentSecurityPolicy.cs) — maps the
  selected UI mode onto normalized file and process effects at the shared
  security authority.
- [`IApprovalPrompt.cs`](IApprovalPrompt.cs) — the terminal interaction used by
  the application's `IApprovalHandler` for exact retained approval requests.
- [`CodingAgentApprovalHandler.cs`](CodingAgentApprovalHandler.cs) — returns an
  authenticated response for the authority-owned request; the selected in-memory
  approval store is explicitly process-local and ephemeral.
- [`CodingAgentHumanQuestionChannel.cs`](CodingAgentHumanQuestionChannel.cs) —
  authenticates one exact terminal selection after the shared broker authorizes
  question publication; the displayed prompt itself carries no authority.
- [`AutoApprovePrompt.cs`](AutoApprovePrompt.cs) — the headless
  `IApprovalPrompt` used by `--smoke-test`; approves everything and prints what
  it approved.
- [`IHumanQuestionPrompt.cs`](IHumanQuestionPrompt.cs),
  [`HumanQuestionSelection.cs`](HumanQuestionSelection.cs),
  [`HumanQuestionSelectionParser.cs`](HumanQuestionSelectionParser.cs), and
  [`UnavailableHumanQuestionPrompt.cs`](UnavailableHumanQuestionPrompt.cs) — the
  terminal side of the human-question tool and its headless stand-in.
- [`CodingAgentApprovalResponderAuthorizer.cs`](CodingAgentApprovalResponderAuthorizer.cs)
  — accepts approval responses only from the local terminal identity.
- [`OpenAiEnvironment.cs`](OpenAiEnvironment.cs) — reads `OPENAI_API_KEY`/
  `CODING_AGENT_MODEL` from the process environment.
- [`DotEnvLoader.cs`](DotEnvLoader.cs) — loads `.env.local` into the process
  environment at startup, without overriding an already-exported variable.
- [`CodingAgentSmokeTest.cs`](CodingAgentSmokeTest.cs) — the headless
  `--smoke-test` path.

[Project catalog](../../docs/packages/index.md)
