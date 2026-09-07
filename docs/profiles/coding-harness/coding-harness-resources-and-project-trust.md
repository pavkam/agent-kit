# Coding-harness resources and project trust

**Status:** Normative application profile

**Scope:** Optional application composition; not a required AgentKit capability.

**Depends on:** [Configuration](../../concepts/configuration-and-overrides.md),
[context assembly](../../concepts/context-assembly-and-instructions.md),
[permissions](../../concepts/permissions-approvals-and-trust.md)

## Purpose

A coding harness discovers configuration, instructions, skills, prompts,
extensions, model catalogs, themes, and tool metadata from many locations. Read
and discovery must be pure; migration, installation, command execution, remote
fetch, and extension loading are separate protected operations.

## Resource model

Each discovered resource has a typed kind, stable source identity, canonical
location, content digest, size, trust class, precedence layer, schema/version,
and diagnostic history. Collision identity and merge behavior are defined per
kind. A filename or front-matter name alone is not globally unique.

Discovery declares:

- global, managed, user, package, repository, directory-ancestry, workspace, and
  invocation locations;
- traversal direction, repository/mount stop conditions, and maximum depth;
- deterministic precedence, disable/reset behavior, and duplicate diagnostics;
- file-count, byte, nesting, symlink, remote-response, and parse limits; and
- the boundary at which a captured immutable catalog becomes visible to runs.

Ancestor and containment checks operate on path components and resolved
workspace identities. Lexical `startsWith` is not ancestry.

## Pure load and explicit maintenance

`Discover`, `Read`, `Parse`, `Validate`, and `Compile` are observationally pure.
They MUST NOT:

- rewrite or migrate source files;
- install packages, language servers, formatters, or plugins;
- run package lifecycle hooks or shell commands;
- fetch remote content unless the selected source is explicitly a protected
  network resource; or
- change credentials, trust, permissions, or workspace state.

When migration or installation is needed, loading returns a typed plan. An
explicit maintenance operation authorizes, executes, audits, and settles that
plan. Invalid candidates leave the last known-good immutable snapshot active.

## Variables, includes, and diagnostics

Variable interpolation distinguishes absent from empty. Missing required
environment variables, secret references, and include files are errors unless a
field explicitly declares a default. Environment lookup uses an allowlist and
records only variable names and redacted provenance.

File includes bind canonical paths, size limits, encoding, and trust. Included
secret content does not appear in expanded-configuration errors, logs, or
diagnostic excerpts. Parse errors point to source spans in the original file;
they do not echo an expanded document containing credentials or private file
contents.

Remote includes bind origin, redirect policy, media type, encoding, maximum
compressed/decompressed bytes, timeout, integrity/ETag policy, cache/expiry, and
failure behavior. Remote text is untrusted data even when successfully fetched.

Template substitution is typed and escaped for its destination. Injected text
cannot break synthetic XML/Markdown delimiters unnoticed. Shell expansion is
never a template feature; a requested command becomes an admitted process/tool
operation before its result is inserted.

### Executable configuration values

A value obtained by running a command is a distinct typed resource, not a magic
prefix accepted by every string setting. Pure configuration loading may compile
its unresolved descriptor, but resolution is a protected process operation with
an exact executable/argument profile, working directory, projected environment,
identity, timeout, output bound, and audit record. A command-backed credential
is resolved by the selected credential source at the provider's send-time
boundary; the generic resource loader never materializes or caches its secret.

Resolution distinguishes absent output, empty output, non-zero exit, timeout,
cancellation, truncation, decoding failure, and start failure. stderr and the
command text follow secret-safe diagnostics policy. Any cache declares whether
successes and failures are retained and keys by resource identity and version,
resolver profile, dependency fingerprints, execution identity/tenant, authority
version, and expiry. Process-global caching by raw command text is forbidden;
refresh, credential rotation, and configuration replacement must invalidate the
appropriate result without leaking it across accounts or workspaces.

## Instructions and nearby context

Instruction discovery records the exact ancestry and precedence used. Nearby
files are eligible only when their canonical directory lies on the configured
workspace ancestry. Instruction text carries source, trust, and delimiters into
the context manifest and never becomes authority.

Repository-controlled instructions cannot override managed security policy,
credentials, destination allowlists, or system-owned context. Conflicts between
same-precedence sources are deterministic and visible rather than dependent on
filesystem enumeration order.

## Command routing and prompt expansion

The input path captures one immutable resource and hook catalog before it
interprets a leading command or resource invocation. Its profile defines this
ordered pipeline:

1. Recognize a typed control/extension command and route it outside the
   conversational queue, or continue with conversational input.
2. Run the named input transformation hook chain over immutable text and content
   parts.
3. Resolve a skill invocation against the captured catalog.
4. Expand a prompt template against that same catalog.
5. Validate final content and bounds, then durably admit it with the original
   caller payload and complete transformation/expansion manifest.

The manifest records every handler, resource identity, version/digest, argument
binding, source span, and resulting content fingerprint. Replaying an existing
`InputId` returns the original admission; it does not rerun hooks or expand
against a newer catalog. Steering and follow-up use this same pipeline, but a
control command illegal for queueing returns a typed `CommandCannotBeQueued`
outcome. An expansion read/parse/validation failure returns a typed failure
before admission and MUST NOT pass the original slash command to the model as if
it were ordinary trusted content.

## Extensions and executable resources

An extension is loadable only after its manifest, entry point, package identity,
version, integrity/source, capabilities, and trust policy validate. File
existence is not a plugin contract. Dynamic import or package execution is a
protected process/file effect and cannot occur while merely listing resources.

Extension factories run with bounded startup context and cancellation. Partial
registration is rolled back. Reload disposes the old contribution before the new
catalog publishes, or reports that process replacement is required.

## Reload and watch behavior

Watchers debounce with `TimeProvider`, produce bounded change sets, and rebuild
the complete affected immutable candidate. Watch overflow or source deletion is
an explicit diagnostic. Current operations retain their captured resource
manifest; a reload affects only named request/session boundaries.

## Acceptance scenarios

- Loading an old config returns a migration plan without changing the file.
- Missing environment variables do not silently become empty strings.
- A parse error never includes expanded secret or file contents.
- Path-component ancestry rejects sibling prefixes such as `project-old` for
  workspace `project`.
- Remote instructions exceeding compressed or expanded size limits fail closed.
- Template text containing delimiters remains unambiguous in the context
  manifest.
- Retrying one input ID after a resource reload returns the original expansion
  rather than silently changing the prompt.
- A skill read failure never turns the unexpanded slash command into model
  input, and an extension command rejected from a queue remains a control
  operation rather than text.
- Command-backed configuration cannot execute during discovery or share one
  process-global cached credential across identities.
- Listing plugins executes no package code.
- Invalid reload preserves the previous catalog for in-flight and new runs
  according to policy.

## Precedence, expansion, trust, and reload requirements

Every AgentKit resource profile decides these collision and trust cases
explicitly:

- within one directory, an explicit override candidate precedes conventional
  instruction filenames and case variants, and exactly one wins; global context
  loads before workspace ancestors ordered root-to-current-directory;
- linked worktrees avoid applying a logically duplicate main-worktree context
  file twice;
- user skills remain discoverable before project trust, while project
  `.agents/skills` ancestry requires trust;
- explicit project, discovered project, explicit user, discovered user, and
  package resources have a deterministic precedence rather than filesystem
  enumeration order;
- disabling default extensions/skills/prompts/themes is distinct from rejecting
  an explicit command-line resource path;
- filters distinguish glob exclusion from exact force-include/force-exclude and
  state how `.gitignore`, `.ignore`, and `.fdignore` participate; and
- prompts/themes, tools, commands, and flags may need different collision
  policies. First-wins, later-wins, and generated disambiguation suffixes cannot
  be one accidental dictionary overwrite rule.

The selected candidate and every shadowed conflict are recorded in the immutable
resource manifest.

Prompt-like input has a deterministic route: registered extension commands run
first when legal at the current lifecycle boundary; the `input` extension event
runs next; skill invocations expand after that; and prompt templates expand
last. Direct steer and follow-up calls reject extension commands but expand
skill and template content before queueing. The admission record captures the
expansion manifest. A skill read failure returns a typed expansion error; it
never passes an unexpanded slash command to the model.

Compatibility profiles may encounter API-key, header, or other configuration
strings beginning with `!` as shell commands. Synchronous execution with a
ten-second timeout, trimmed stdout, ignored stderr, failure collapsed to
`undefined`, and process-lifetime caching by raw command text are unsafe
defaults. Command-backed values instead use the protected typed resolver and
explicit failure, output, timeout, identity, and cache-scope rules above.

Trust bootstraps in two phases. Managed/user and explicitly supplied trusted
extensions may contribute the trust UI/policy first; repository settings,
packages, skills, and extensions load only after the workspace decision. In a
noninteractive channel, `Ask` cannot block invisibly and resolves through an
explicit deny/approval policy. Trusting a parent path during an already built
runtime either triggers a named rebuild or clearly requires restart.

A reload is a lifecycle transaction: notify and tear down the old session
generation, invalidate captured extension contexts, drain settings writes,
capture new settings/provider/resource catalogs, clear module generations,
rebuild tools and contributions, publish the new generation, and emit the new
session/start boundary. Selected flag values and active-tool intent may migrate
only through a declared compatibility transform. Old subscriptions, background
jobs, and callable contexts cannot survive merely because their objects remain
reachable.

Recursive discovery, swallowed diagnostics, install-capable maintenance paths,
and non-atomic settings writes are forbidden. The profile requires bounded
symlink-aware traversal, pure discovery, explicit maintenance, atomic settings
replacement, last-known-good publication, and concurrent-creation safety.

Configuration loading performs neither migration nor installation. Missing
variables remain diagnosed missing values, parse diagnostics never expose
expanded secrets, remote fetches are bounded and protected, path ancestry uses
canonical components rather than lexical prefixes, and prompt expansion cannot
execute a shell outside the process security boundary.

## Related specifications

- [Language services, formatters, and watchers](language-services-formatters-and-watchers.md)
- [Coding workspaces and worktrees](coding-workspaces-and-worktrees.md)
- [Extensions, hooks, and middleware](../../concepts/extensions-hooks-and-middleware.md)
- [Provider request pipeline](../../concepts/provider-request-pipeline.md)
