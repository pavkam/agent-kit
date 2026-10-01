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
  total (from the real `ConversationUsageEvent` the model provider reports) and
  a live todo checklist (☐ pending, ◐ in progress, ☑ completed, ✗ blocked),
  parsed straight from the `todo` tool's own results as the agent works through
  a multi-step task. The system prompt tells the model to use the todo tool for
  anything beyond a couple of tool calls.
- **The status bar carries live context**: a compact tokens/cost summary, the
  current model id, and the workspace's directory name sit right-aligned,
  separated from the busy/ready indicator so you never lose track of what a
  session has cost or which agent/workspace it belongs to.

### Navigation and persistence

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

[Example: CodingAgent](../../docs/use-cases/coding-agent-example.md) walks
through the composition in `AgentRuntime.cs` block by block, and the
[use cases](../../docs/use-cases/index.md) take individual slices of it
(approval before writes, sandboxed commands, human questions) and rebuild them
on the builder sugar.

`AgentRuntime.cs` composes an `AgentEngine` with one pinned `AgentDefinition`
that selects every required collaborator by key, and `AddConversationSession`
takes that definition and its `EffectiveConfigurationSnapshot` instead of
repeating the model policy, instructions, tools, and limits. `AgentRuntimeTests`
builds the whole composition, so engine validation failures surface in the test
run. The pieces it exercises end to end are the ones a real conversational turn
needs: session creation, message admission, a multi-turn tool-calling loop with
bounded rebasing when a tool commits its own session entries, interrupted
terminal results for cancelled tool calls, per-call approval through the
security authority, and a real provider round trip.

Driving the example against a substantial repository also taught two lessons
about the tools themselves, both reflected in the system prompt in
`AgentRuntime.cs`:

- `glob` and `search` patterns filter only after the walk visits an entry;
  `base_path` is the only argument that limits traversal. The prompt tells the
  model to glob a shallow `base_path` first and recurse into a specific project
  directory only afterwards.
- Multi-line tool output rendered as Markdown prose loses its line breaks, so
  tool calls, results, and file content render through `CodeView`, which
  preserves line structure and adds syntax color where the bundled catalog
  covers the language. `Document`/Markdown is reserved for the assistant's and
  the user's prose.

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
