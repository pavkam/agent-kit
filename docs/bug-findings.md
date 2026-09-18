# Source bug review — findings backlog

A read-only defect review of every library under `src/`, run on 2026-09-17
against `main` at `4c1454e`. Ten review passes, each covering one cohesive group
of packages, read the implementation together with its mirror tests, the
contracts in `AgentKit.Abstractions`, and the invariants in `AGENTS.md`, and
reported only defects they could evidence in code: correctness, concurrency,
resource leaks, security, validation gaps, async misuse, serialization loss, and
contract/documentation mismatches. Style, naming, and refactoring ideas were out
of scope.

Nothing here has been fixed yet. This file is the backlog to work from; each
finding carries the file and line it was observed at, an evidence excerpt, a
suggested fix, and the reviewer's confidence. Line numbers refer to the commit
above and will drift.

**Totals: 92 findings — High 4, Medium 41, Low 47.**

## Triage table

Sorted by severity, then by ID. The full write-up for each ID is in the group
sections below.

| ID  | Severity | Package                                                                                                                    | Category          | Title                                                                                                                                                                           |
| --- | -------- | -------------------------------------------------------------------------------------------------------------------------- | ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A01 | High     | AgentKit.Abstractions                                                                                                      | security          | Credential records leak secrets through synthesized `ToString`/`PrintMembers`                                                                                                   |
| H01 | High     | AgentKit.FileSystem                                                                                                        | correctness       | Raw libc `fstat`/`readdir` struct offsets are only correct for x86_64 Linux and arm64 macOS (wrong on x86_64 macOS and aarch64 Linux)                                           |
| H02 | High     | AgentKit.FileSystem                                                                                                        | contract-mismatch | `CreateOrOverwrite` on an existing file truncates in place; not atomic and leaves an empty/partial target on failure or cancellation                                            |
| P01 | High     | AgentKit.Providers.OpenAICompatible (affects AgentKit.Providers.OpenRouter)                                                | serialization     | OpenRouter error payloads carry a numeric `error.code`, which fails DTO deserialization and misclassifies every OpenRouter error                                                |
| A02 | Medium   | AgentKit.Abstractions                                                                                                      | validation        | `SecurityGrant` invariants can be bypassed through `with`/`init` on unvalidated properties                                                                                      |
| A03 | Medium   | AgentKit.Abstractions                                                                                                      | validation        | `SecurityGrant`/`SecurityRequest` accept default identities, audience, and fingerprint                                                                                          |
| A04 | Medium   | AgentKit.Abstractions                                                                                                      | security          | IPv6 literal hosts produce inconsistent `ProtectedResource` identifiers across network binding paths                                                                            |
| A05 | Medium   | AgentKit.Abstractions                                                                                                      | contract-mismatch | `NetworkRequest` documents structural equality but compares `ResolvedAddresses` by reference                                                                                    |
| A06 | Medium   | AgentKit.Abstractions                                                                                                      | correctness       | `SequencingModelResponseObserver` marks the attempt started before delivery succeeds                                                                                            |
| A07 | Medium   | AgentKit.Abstractions                                                                                                      | contract-mismatch | `JsonDurableOperationCodec.Decode` can throw for unreadable payloads despite the non-throwing contract                                                                          |
| A08 | Medium   | AgentKit.Abstractions                                                                                                      | validation        | `AgentRunRequest` `init` setters let a request diverge from its pinned `AgentDefinition`                                                                                        |
| B01 | Medium   | AgentKit.Permissions                                                                                                       | contract-mismatch | SecurityAuthority emits no audit record for requests, decisions, denials, or non-approval grants                                                                                |
| B02 | Medium   | AgentKit.Budgets.InMemory                                                                                                  | correctness       | In-memory hold clearance and hard-failure checks count expired-but-unswept unstarted reservations as retained capacity                                                          |
| B03 | Medium   | AgentKit.Budgets.Sqlite                                                                                                    | correctness       | SQLite hold clearance and hard-failure checks read `projection.Reserved`, which still includes expired-but-unswept unstarted reservations                                       |
| F01 | Medium   | AgentKit                                                                                                                   | validation        | Composition validation does not verify the per-run collaborator bundle; missing services surface only inside `RunAsync`                                                         |
| F02 | Medium   | AgentKit.Hooks                                                                                                             | correctness       | Hook dispatcher lets observability failures fail the dispatch and skip isolated-hook rollback                                                                                   |
| F03 | Medium   | AgentKit.Goals                                                                                                             | async             | Delegation broker throws `OperationCanceledException` after the child was successfully dispatched, discarding the result                                                        |
| F04 | Medium   | AgentKit.Simple                                                                                                            | correctness       | `SimpleAgentPlan` mints instruction messages and the local identity from ambient clock/GUIDs, so the catalog definition and conversation options disagree                       |
| H03 | Medium   | AgentKit.FileSystem                                                                                                        | async             | `ReadAsync`/`WriteAsync` open the target without `O_NONBLOCK`, so a FIFO inside the workspace hangs the operation uncancellably                                                 |
| H04 | Medium   | AgentKit.Processes                                                                                                         | async             | Standard-input delivery is not covered by the operation timeout; a child that never reads stdin hangs the run indefinitely                                                      |
| H05 | Medium   | AgentKit.Network                                                                                                           | security          | Pooled HTTP connections are keyed by host:port, so a later request can be served over a connection to an address that is not the request's pinned, still-valid resolved address |
| H06 | Medium   | AgentKit.Network                                                                                                           | security          | Caller-supplied `Host` header is forwarded verbatim, overriding the HTTP virtual host and TLS SNI/certificate target without re-enforcement against the destination allow-list  |
| H07 | Medium   | AgentKit.FileSystem.InMemory                                                                                               | contract-mismatch | In-memory Glob/Search with a non-null `BasePath` double-prefix result paths and match patterns/exclusions against the full workspace path instead of the base-relative path     |
| L01 | Medium   | AgentKit.Conversations                                                                                                     | contract-mismatch | Conversation session mints idempotency keys with `Guid.NewGuid()`, bypassing injected identity and defeating idempotency                                                        |
| L02 | Medium   | AgentKit.Conversations                                                                                                     | async             | Loop scope is disposed synchronously; an `IAsyncDisposable`-only scoped loop throws after the turn already committed                                                            |
| M01 | Medium   | AgentKit.Mcp                                                                                                               | correctness       | `McpToolContract<TTools>.Resolve` rejects inherited (non-overridden) attributed methods because `MethodInfo` identity includes `ReflectedType`                                  |
| M02 | Medium   | AgentKit.Mcp.Client                                                                                                        | contract-mismatch | `McpClientVersionPolicy.RequireAtLeast` pins negotiation to exactly the "minimum" revision (never a newer one)                                                                  |
| M03 | Medium   | AgentKit.Context.Compaction                                                                                                | correctness       | Compaction source ceiling and transcript are computed over the whole branch prefix, so compaction becomes permanently impossible past `MaximumSourceEntries`                    |
| N01 | Medium   | AgentKit.Providers.MistralAI                                                                                               | contract-mismatch | Mistral parser silently keeps only `choices[0]` and merges streamed choices without checking `index`                                                                            |
| N02 | Medium   | AgentKit.Providers.GoogleGemini                                                                                            | contract-mismatch | Gemini parser silently keeps only `candidates[0]` and merges streamed candidates                                                                                                |
| N03 | Medium   | AgentKit.Providers.GoogleGemini                                                                                            | contract-mismatch | Gemini embedding space identity discards the requested `taskType`                                                                                                               |
| N04 | Medium   | AgentKit.Providers.GoogleVertexAI                                                                                          | contract-mismatch | Vertex AI embedding space identity discards the requested `task_type`                                                                                                           |
| N05 | Medium   | AgentKit.Providers.AwsBedrock                                                                                              | correctness       | Bedrock model-ID path escaping leaves `/` unescaped, breaking ARN model IDs                                                                                                     |
| N06 | Medium   | AgentKit.Providers.AwsBedrock                                                                                              | contract-mismatch | Bedrock translator emits consecutive same-role messages that Converse rejects                                                                                                   |
| P02 | Medium   | AgentKit.Providers.OpenAICompatible                                                                                        | contract-mismatch | Buffered parser throws `ArgumentException` (no terminal event) on empty/whitespace tool-call `id`/`name` or whitespace `model`/`id`                                             |
| P03 | Medium   | AgentKit.Providers                                                                                                         | contract-mismatch | `DefaultModelSelector` accepts a `CapabilitiesDowngraded` candidate but the declared adjustment is dropped, so a parallel-tool-call downgrade later fails preflight             |
| P04 | Medium   | AgentKit.Providers.OpenAICompatible (all leaves)                                                                           | validation        | Base address without trailing slash silently drops its last path segment when the operation path is combined                                                                    |
| P05 | Medium   | AgentKit.Providers.OpenAICompatible                                                                                        | contract-mismatch | Credential-source failures escape `ExecuteAsync`/`GenerateAsync` as raw exceptions instead of a typed `Authentication` failure                                                  |
| P06 | Medium   | AgentKit.Abstractions (contract consumed by every leaf's `Add<Provider>ApiKeyCredential` / `StaticApiKeyCredentialSource`) | security          | `ApiKeyProviderCredential` / `OAuthTokenProviderCredential` are plain records: default `ToString()` prints the secret                                                           |
| S01 | Medium   | AgentKit.Session.Sqlite                                                                                                    | validation        | Appended entries' `Address`/`BranchId` are never validated against the request                                                                                                  |
| S02 | Medium   | AgentKit.Session                                                                                                           | concurrency       | Durable lane release CASes on a session version read in a separate transaction and never retries                                                                                |
| T01 | Medium   | AgentKit.Tools                                                                                                             | contract-mismatch | Tool arguments are never validated against the compiled schema before invocation                                                                                                |
| T02 | Medium   | AgentKit.Tools                                                                                                             | validation        | Merge-graph `Apply` lets a policy bind an authored alias to a tool it was never authored for                                                                                    |
| T03 | Medium   | AgentKit.Tools.Patch                                                                                                       | correctness       | Patch hunks with a `\ No newline at end of file` tail are not end-anchored and can match a line prefix                                                                          |
| T04 | Medium   | AgentKit.Tools.Plan                                                                                                        | contract-mismatch | `set_status` on a session with no plan is reported as a successful, performed mutation                                                                                          |
| A09 | Low      | AgentKit.Abstractions                                                                                                      | validation        | `NetworkDestinationPolicy` throws `NullReferenceException` for null/blank schemes and misses NAT64-embedded IPv4                                                                |
| A10 | Low      | AgentKit.Abstractions                                                                                                      | validation        | `ProcessEnvironmentVariable` accepts names containing `=`                                                                                                                       |
| A11 | Low      | AgentKit.Abstractions                                                                                                      | correctness       | `LlmRequestSettings` equality is non-reflexive for NaN and accepts NaN/±Infinity sampling parameters                                                                            |
| A12 | Low      | AgentKit.Abstractions                                                                                                      | validation        | `ModelResponse` allows null `ContentPart` elements                                                                                                                              |
| A13 | Low      | AgentKit.Abstractions                                                                                                      | validation        | `NormalizedHost` surfaces `IdnMapping` failures with a foreign `ParamName`                                                                                                      |
| A14 | Low      | AgentKit.Abstractions                                                                                                      | validation        | `BudgetReservationRequest.Amount` `init` bypasses the positive-amount invariant                                                                                                 |
| A15 | Low      | AgentKit.Abstractions                                                                                                      | correctness       | `MediaReference` equality ignores URI fragments and inline size consistency                                                                                                     |
| A16 | Low      | AgentKit.Abstractions                                                                                                      | validation        | `DelegatingOAuthCredentialSource` propagates a null credential from a misbehaving provider                                                                                      |
| B04 | Low      | AgentKit.Permissions                                                                                                       | async             | Approval broker never bounds the handler wait by the approval binding's expiry                                                                                                  |
| F05 | Low      | AgentKit.Context                                                                                                           | correctness       | `DefaultContextAssembler` lets metric/activity failures replace the typed result                                                                                                |
| F06 | Low      | AgentKit                                                                                                                   | resource-leak     | `Build()` failure path disposes the provider synchronously, which throws and masks the real error for `IAsyncDisposable`-only services                                          |
| F07 | Low      | AgentKit                                                                                                                   | contract-mismatch | `Agent.RunAsync` documents `ObjectDisposedException` but the engine performs security capture before any disposed check, and hosted engines never throw                         |
| F08 | Low      | AgentKit                                                                                                                   | contract-mismatch | Resolving `AgentEngine` from a standalone engine's `Services` yields a second, unowned engine                                                                                   |
| F09 | Low      | AgentKit.Context                                                                                                           | validation        | Instructions bypass state and tool-part validation in `DefaultContextAssembler`                                                                                                 |
| F10 | Low      | AgentKit.Identity                                                                                                          | validation        | `AddAgentIdentity` options validation is not run at startup; invalid values surface as `ArgumentOutOfRangeException` on first identity use                                      |
| F11 | Low      | AgentKit.Simple                                                                                                            | correctness       | `UseSqliteSessions` performs a file-system mutation during registration                                                                                                         |
| H08 | Low      | AgentKit.FileSystem                                                                                                        | validation        | Whitespace-only entry names make Enumerate/Glob/Search throw `ArgumentException` instead of returning a typed failure                                                           |
| H09 | Low      | AgentKit.Processes                                                                                                         | concurrency       | A zero `TerminationGracePeriod` makes the post-exit output drain race with EOF and report a clean exit as `Failed`                                                              |
| H10 | Low      | AgentKit.Processes                                                                                                         | concurrency       | Raw `kill(pid, SIGKILL)` after the tree kill can target a reused PID once the child has been reaped                                                                             |
| L03 | Low      | AgentKit.IO                                                                                                                | concurrency       | `CompleteAsync`/`DisposeAsync` race leaves a retained final result whose `Completion` is cancelled                                                                              |
| L04 | Low      | AgentKit.Conversations                                                                                                     | correctness       | Non-completed run outcomes are logged/metered as "admission failed"                                                                                                             |
| L05 | Low      | AgentKit.Output                                                                                                            | correctness       | Runtime-type deserialization only catches `JsonException`; unsupported runtime types escape as run faults                                                                       |
| L06 | Low      | AgentKit.Loop                                                                                                              | contract-mismatch | Zero-effect caller cancellation is surfaced two different ways depending on adapter behaviour                                                                                   |
| L07 | Low      | AgentKit.Loop                                                                                                              | correctness       | Dangling-tool-call recovery derives its idempotency key and causal parent from the wrong assistant message                                                                      |
| M04 | Low      | AgentKit.Context.Compaction                                                                                                | contract-mismatch | `DefaultCompactor` never populates `CompactionRecord.Supersedes` even when the cut covers an earlier active record                                                              |
| M05 | Low      | AgentKit.Mcp.Client                                                                                                        | resource-leak     | MCP client leaks the connected SDK session if `SdkMcpToolCaller` construction throws after `McpClient.CreateAsync` succeeds                                                     |
| M06 | Low      | AgentKit.Durability.InMemory                                                                                               | correctness       | Journal writes mutate state before the fallible `GetUtcNow()` call, so a clock failure reports an exception for a committed write                                               |
| N07 | Low      | AgentKit.Providers.AwsBedrock                                                                                              | correctness       | Bedrock event-stream `:message-type: error` frames are misreported as malformed JSON                                                                                            |
| N08 | Low      | AgentKit.Providers.Anthropic                                                                                               | correctness       | Anthropic streaming: `input_json_delta` on a non-`tool_use` block throws instead of failing typed                                                                               |
| N09 | Low      | AgentKit.Providers.AwsBedrock                                                                                              | correctness       | Bedrock streaming: `toolUse` delta on a text accumulator throws instead of failing typed                                                                                        |
| N10 | Low      | AgentKit.Providers.Cohere                                                                                                  | correctness       | Cohere `message-end` error text is parsed but discarded; stream errors surface as completed responses                                                                           |
| N11 | Low      | AgentKit.Providers.GoogleGemini                                                                                            | contract-mismatch | Gemini 429 `RESOURCE_EXHAUSTED` retry hint (`RetryInfo.retryDelay`) is not parsed                                                                                               |
| N12 | Low      | AgentKit.Providers.Anthropic (identical registration in Cohere, MistralAI, AwsBedrock, GoogleGemini, GoogleVertexAI)       | correctness       | Default `HttpClient` registration keeps the 100 s `HttpClient.Timeout`, silently capping non-streaming attempts below the request deadline                                      |
| P07 | Low      | AgentKit.Providers.OpenAICompatible                                                                                        | contract-mismatch | Embedding parser throws `InvalidOperationException` (uncaught) when a vector element is not a JSON number                                                                       |
| P08 | Low      | AgentKit.Providers.OpenAICompatible                                                                                        | validation        | A request deadline more than ~49.7 days away makes the deadline `CancellationTokenSource` constructor throw                                                                     |
| P09 | Low      | AgentKit.Providers                                                                                                         | validation        | LLM preflight ignores `SupportsReasoning`/`SupportsSystemInstructions`, so `reasoning_effort` and `system`/`developer` messages are sent to models that declare no support      |
| P10 | Low      | AgentKit.Providers.OpenAICompatible                                                                                        | validation        | Embedding translator ignores `EmbeddingCapabilities` (`SupportsDimensions`, `SupportsEncodingSelection`)                                                                        |
| P11 | Low      | AgentKit.Providers.OpenAICompatible (affects AgentKit.Providers.XAI)                                                       | serialization     | Error bodies whose `error` member is a string (xAI shape) throw during error-body parsing and lose the provider code/message                                                    |
| P12 | Low      | AgentKit.Providers.OpenAI (identical in AzureOpenAI, OpenRouter, Ollama, XAI, DeepSeek, MoonshotKimi, ZAI, Groq)           | resource-leak     | Every leaf registers a raw `new HttpClient()` singleton (infinite pooled-connection lifetime, no factory/resilience, first-registration wins)                                   |
| S03 | Low      | AgentKit.Session                                                                                                           | resource-leak     | Lane slots are never removed from `_slots`, so the coordinator grows without bound                                                                                              |
| S04 | Low      | AgentKit.Session                                                                                                           | correctness       | `Outcome` classifier omits release and directory-list results, so successful releases/lists are recorded as failed/"unknown"                                                    |
| S05 | Low      | AgentKit.Session.Sqlite                                                                                                    | correctness       | Directory listing loads every route for the tenant/agent per page; `MaximumResults` and `AfterSessionId` are not pushed into SQL                                                |
| S06 | Low      | AgentKit.Session.Sqlite                                                                                                    | serialization     | Corrupt persisted value objects surface as `TargetInvocationException` instead of `JsonException`                                                                               |
| T05 | Low      | AgentKit.Tools.Read                                                                                                        | correctness       | Read window treats a trailing newline as an extra empty line, misreporting `complete`                                                                                           |
| T06 | Low      | AgentKit.Tools.Web                                                                                                         | security          | Redirect targets bypass the tool's own URL admission checks                                                                                                                     |
| T07 | Low      | AgentKit.Tools.Web (also Tools.WebSearch, Tools.Skill, Tools.Resource)                                                     | correctness       | Character truncation can split a surrogate pair, emitting a lone surrogate                                                                                                      |
| T08 | Low      | AgentKit.Tools                                                                                                             | validation        | `AddAgentTools` and `AddTool<TTool>` omit the `services` null guard required at public boundaries                                                                               |

## Findings by group

## Group 01 — AgentKit.Abstractions

Read-only review of `src/AgentKit.Abstractions/` (contract package: identities,
records, guard extensions, security/budget/session/tool value types, provider
primitives). The package is generally careful: identities are validating
`readonly record struct`s, most collection-bearing records override
`Equals`/`GetHashCode`, all async contracts take a `CancellationToken`, no
ambient clocks or mutable statics exist, and flags enums are never passed to
`Enum.IsDefined`. The defects found cluster around (a) secret-bearing records
with compiler-synthesized `ToString`, (b) `with`/`init` bypasses of constructor
invariants on security- and run-critical records, (c) documented contracts that
the code does not enforce (structural equality, non-throwing decode, "state
updated only for delivered events"), and (d) canonicalization inconsistencies in
security resource identifiers. Totals: **1 High, 7 Medium, 8 Low** (16
findings).

---

### A01 — Credential records leak secrets through synthesized `ToString`/`PrintMembers`

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Providers/ApiKeyProviderCredential.cs:33`
  (also `Providers/OAuthTokenProviderCredential.cs:51`)
- **Severity:** High
- **Category:** security
- **Description:** `ApiKeyProviderCredential` and `OAuthTokenProviderCredential`
  are `sealed record` types with no `ToString()` or `PrintMembers` override. The
  compiler-generated `ToString()` renders
  `ApiKeyProviderCredential { ApiKey = sk-live-... }`. Any structured log,
  exception message, debugger display, `Shouldly` failure message, or string
  interpolation that touches a `ProviderCredential` (returned by every
  `IProviderCredentialSource`) will print the raw secret, violating the
  repository rule that credentials never enter logs or diagnostics. Nothing in
  the package or tests asserts redaction.
- **Evidence:**

  ```csharp
  public sealed record ApiKeyProviderCredential: ProviderCredential
  {
      public ApiKeyProviderCredential(string apiKey)
      {
          ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
          ApiKey = apiKey;
      }
      public string ApiKey { get; init; }
  }
  ```

- **Suggested fix:** Override `ToString()` (and `PrintMembers`) on
  `ProviderCredential`/both leaves to emit a redacted form (type name plus
  length or fingerprint), and add tests asserting the secret text is absent from
  `ToString()`. Consider also making the `init` setters validate non-blank.
- **Confidence:** High
- **Status:** Fixed ✅

### A02 — `SecurityGrant` invariants can be bypassed through `with`/`init` on unvalidated properties

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Security/SecurityGrant.cs:136`
- **Severity:** Medium
- **Category:** validation
- **Description:** The constructor validates `kind`/`effect` (defined),
  `resources` (non-default, non-empty), `allowedUses` (positive), and
  `expiresAt > notBefore`, but `Kind`, `Effect`, `Resources`, `AllowedUses`,
  `NotBefore`, `ExpiresAt`, `Audience`, `InputFingerprint`, `Id`, and
  `RequestId` are plain `{ get; init; }`. `grant with { Resources = default }`,
  `with { AllowedUses = 0 }`, `with { ExpiresAt = DateTimeOffset.MinValue }`, or
  `with { Effect = (SecurityEffect)999 }` produce a grant that violates every
  documented constraint; a default `Resources` then makes `Equals`/`GetHashCode`
  throw on enumeration. The same record deliberately validates
  `Scope`/`Identity`/`PolicyVersion` in their `init` accessors, so the omission
  is inconsistent within the type. `SecurityRequest`
  (`Security/SecurityRequest.cs:115-127`) has the identical gap for `Audience`,
  `Kind`, `Effect`, `Resources`, `RequestedUses`.
- **Evidence:**

  ```csharp
  public SecurityEffect Effect { get; init; }
  /// <summary>Gets the ordered canonical resources.</summary>
  public ImmutableArray<ProtectedResource> Resources { get; init; }
  ...
  public DateTimeOffset ExpiresAt { get; init; }
  /// <summary>Gets the maximum successful consumption count.</summary>
  public int AllowedUses { get; init; }
  ```

- **Suggested fix:** Either make these properties `{ get; }` (the record is
  described as immutable evidence) or move each constructor guard into the
  corresponding `init` accessor as already done for
  `Scope`/`Identity`/`PolicyVersion`; cross-field checks
  (`ExpiresAt > NotBefore`) need a validating factory instead of `with`.
- **Confidence:** High
- **Status:** Fixed ✅

### A03 — `SecurityGrant`/`SecurityRequest` accept default identities, audience, and fingerprint

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Security/SecurityGrant.cs:51`
- **Severity:** Medium
- **Category:** validation
- **Description:** The constructor never checks `id`, `requestId`, `audience`,
  or `inputFingerprint` for `default`. A `GrantId`/`SecurityRequestId` of
  `Guid.Empty`, a `ComponentId` with `Value == null`, or an `InputFingerprint`
  with `Value == null` are all accepted, so a grant can be persisted with an
  empty identity (colliding in grant stores) or a null audience/fingerprint that
  an enforcing component then compares with `==` against a real value. Every
  other authority-bearing record in the package (e.g. `SessionOperationContext`,
  `AdmittedInput`, `RunUsage`) rejects default identities at construction.
  `SecurityRequest` shares the gap for `id`, `audience`, `inputFingerprint`.
- **Evidence:**

  ```csharp
  ArgumentNullException.ThrowIfNull(scope);
  ArgumentNullException.ThrowIfNull(identity);
  ArgumentOutOfRangeException.ThrowIfUndefined(kind);
  ArgumentOutOfRangeException.ThrowIfUndefined(effect);
  ArgumentOutOfRangeException.ThrowIfNegativeOrZero(allowedUses);
  ArgumentException.ThrowIfDefaultOrEmpty(resources);
  ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, notBefore);

  Id = id;
  RequestId = requestId;
  ```

- **Suggested fix:** Add
  `ArgumentOutOfRangeException.ThrowIfEqual(id, default)`,
  `ThrowIfEqual(requestId, default)`,
  `ArgumentException.ThrowIfNullOrWhiteSpace(audience.Value, nameof(audience))`,
  and
  `ThrowIfNullOrWhiteSpace(inputFingerprint.Value, nameof(inputFingerprint))` in
  both records and document the exceptions.
- **Confidence:** High
- **Status:** Fixed ✅

### A04 — IPv6 literal hosts produce inconsistent `ProtectedResource` identifiers across network binding paths

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Network/NetworkSecurityBinding.cs:20`
- **Severity:** Medium
- **Category:** security
- **Description:** `NetworkDestination.ToString()` brackets IPv6 hosts
  (`https://[::1]:443/p`), and `RequestResourceIdentifier` returns exactly that
  when the route has no query. But `ResolutionResource` and the query branch of
  `RequestResourceIdentifier` interpolate `destination.Host` directly, yielding
  `https://::1:443` and `https://::1:443/p?query=…`. The same destination is
  therefore identified three different ways depending on which operation and
  whether a `?` is present, so a policy that matches network endpoints by
  prefix/exact identifier will allow or deny inconsistently for IPv6 literals
  (and `::1:443` is ambiguous with a longer IPv6 address).
- **Evidence:**

  ```csharp
  return new ProtectedResource(
      ProtectedResourceKind.NetworkEndpoint,
      $"{destination.Scheme}://{destination.Host}:{destination.Port}");
  ...
  var queryFingerprint = ProcessSecurityBinding.FingerprintText(route[(queryStart + 1)..]);
  return $"{destination.Scheme}://{destination.Host}:{destination.Port}{path}?query={queryFingerprint}";
  ```

- **Suggested fix:** Route all identifier construction through one helper that
  uses the bracketed authority form (the existing `AuthorityHost` logic in
  `NetworkDestination`), and add a test with an IPv6 literal destination
  covering resolution, send-without-query, and send-with-query.
- **Confidence:** High
- **Status:** Fixed ✅

### A05 — `NetworkRequest` documents structural equality but compares `ResolvedAddresses` by reference

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Network/NetworkRequest.cs:75`
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** The remarks state "immutable value object with structural
  equality over its fields", but the record does not override
  `Equals`/`GetHashCode` and contains
  `ImmutableArray<NetworkAddress> ResolvedAddresses`. `ImmutableArray<T>`
  equality is reference equality of the backing array, so two requests built
  from identical inputs are unequal and hash differently. The same pattern
  (record with `ImmutableArray` property and no custom equality) exists in ~45
  records including `AgentInput` (`Input/AgentInput.cs:39`),
  `AgentCatalogSnapshot`, `ModelCatalogSnapshot`, `SessionRunStartRequest`,
  `SessionAcceptedRunState`, `InputPromotionSnapshot`,
  `CommittedTurnContinuationBoundary`, `RunContinuationContext`;
  `NetworkRequest` is the one whose XML documentation explicitly promises
  structural equality.
- **Evidence:**

  ```csharp
  /// This type is an immutable value object with structural equality over its
  /// fields. It carries no mutable state and is safe to share across threads
  ...
  public ImmutableArray<NetworkAddress> ResolvedAddresses { get; }
  ```

- **Suggested fix:** Implement `Equals`/`GetHashCode` using `SequenceEqual` for
  the array (as done in `NetworkHeaderSet`, `SecurityRequest`, etc.), or correct
  the documentation; audit the other listed records and add structural equality
  where callers or docs rely on it.
- **Confidence:** High
- **Status:** Fixed ✅

### A06 — `SequencingModelResponseObserver` marks the attempt started before delivery succeeds

- **Package:** AgentKit.Abstractions
- **File:**
  `src/AgentKit.Abstractions/Providers/SequencingModelResponseObserver.cs:79`
- **Severity:** Medium
- **Category:** correctness
- **Description:** The class documents that "retained state has been updated
  only for events that were actually delivered" and that it "suppresses any
  `ModelResponseStarted` after the first". `HasStarted = true` is set before
  `await _inner.OnEventAsync(...)`. If the inner observer throws or the token is
  cancelled during that first delivery, `HasStarted` stays `true` while
  `NextSequence` is still `0` and the inner observer never received a start; a
  subsequent (retried) `ModelResponseStarted` is silently dropped, so the
  downstream stream begins with a non-start event at sequence 0, violating the
  ordering contract the wrapper exists to guarantee.
- **Evidence:**

  ```csharp
  if (responseEvent is ModelResponseStarted)
  {
      if (HasStarted) { return; }
      HasStarted = true;
  }
  var renumbered = responseEvent with { Sequence = NextSequence };
  await _inner.OnEventAsync(renumbered, cancellationToken).ConfigureAwait(false);
  NextSequence++;
  ```

- **Suggested fix:** Set `HasStarted = true` after the await succeeds (in the
  `switch`, alongside the other post-delivery state updates), and add a test
  where the inner observer throws on the first start and a retried start is then
  delivered.
- **Confidence:** High
- **Status:** Fixed ✅

### A07 — `JsonDurableOperationCodec.Decode` can throw for unreadable payloads despite the non-throwing contract

- **Package:** AgentKit.Abstractions
- **File:**
  `src/AgentKit.Abstractions/Durability/JsonDurableOperationCodec.cs:102`
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** `IDurableOperationCodec<TState>.Decode` documents that "an
  unreadable payload returns the incompatible result rather than throwing" and
  lists only `ArgumentNullException`. The implementation catches `JsonException`
  only. `JsonSerializer.Deserialize` also throws `NotSupportedException` (type
  or member not supported, e.g. `Type`, abstract types without polymorphism, or
  the same conditions the `Encode` path already maps) and
  `InvalidOperationException` (constructor/options misconfiguration). Those
  escape `Decode`, so recovery over an existing journal crashes instead of
  receiving `DurableDecodeIncompatible<TState>` that it "must handle
  deliberately". `Encode` handles `NotSupportedException`; `Decode` is
  asymmetric.
- **Evidence:**

  ```csharp
  try
  {
      var state = JsonSerializer.Deserialize<TState>(payload.Data.AsSpan(), _serializerOptions);
      return new DurableDecoded<TState>(state!);
  }
  catch (JsonException)
  {
      return new DurableDecodeIncompatible<TState>(payload.SchemaVersion, "...");
  }
  ```

- **Suggested fix:** Also catch `NotSupportedException` (and
  `InvalidOperationException` from the serializer) and map to
  `DurableDecodeIncompatible<TState>`, or document the additional exceptions on
  the interface; add a test with `Codec<Type>()` decoding a valid payload.
- **Confidence:** High
- **Status:** Fixed ✅

### A08 — `AgentRunRequest` `init` setters let a request diverge from its pinned `AgentDefinition`

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Loop/AgentRunRequest.cs:200`
- **Severity:** Medium
- **Category:** validation
- **Description:** The evidence-aware constructor derives `ModelPolicy`,
  `ModelRequirements`, `Instructions`, `Tools`, `ToolChoice`, `Settings` from
  `agent` and validates `maxTurns > 0`, `attemptTimeout > 0`, non-default arrays
  and non-null references. All of these are `{ get; init; }` with no guard, so
  `request with { ModelPolicy = other }` keeps `Agent` (the "exact admitted
  agent definition") while the effective policy differs,
  `with { Instructions = default }` makes `Equals` throw, and
  `with { MaxTurns = 0 }` / `with { AttemptTimeout = TimeSpan.Zero }` produce
  runs with impossible limits that the composition boundary was supposed to
  reject.
- **Evidence:**

  ```csharp
  public ModelSelectionPolicy ModelPolicy { get; init; }
  public ModelRequirements ModelRequirements { get; init; }
  public ImmutableArray<AgentMessage> Instructions { get; init; }
  public ImmutableArray<LlmToolDefinition> Tools { get; init; }
  ...
  public int MaxTurns { get; init; }
  public TimeSpan AttemptTimeout { get; init; }
  ```

- **Suggested fix:** Convert to get-only properties, or add validating `init`
  accessors (null/default/range checks) and reject overrides of
  definition-derived members when `Agent` is non-null.
- **Confidence:** High
- **Status:** Fixed ✅

### A09 — `NetworkDestinationPolicy` throws `NullReferenceException` for null/blank schemes and misses NAT64-embedded IPv4

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Network/NetworkDestinationPolicy.cs:70`
- **Severity:** Low
- **Category:** validation
- **Description:** `allowedSchemes` is checked only for default/empty; a null
  element reaches `scheme.ToLowerInvariant()` and surfaces as
  `NullReferenceException` instead of an `ArgumentException`, and blank schemes
  are accepted (never matching, silently useless). Separately,
  `IsPrivateOrLoopback` correctly unwraps IPv4-mapped IPv6 (`::ffff:a.b.c.d`)
  but not NAT64 well-known-prefix addresses (`64:ff9b::/96`), which also connect
  to the embedded IPv4 target; a private RFC1918 address embedded via NAT64
  passes `AllowsAddress` with `AllowPrivateAddresses == false`.
- **Evidence:**

  ```csharp
  AllowedSchemes = [.. allowedSchemes.Select(static scheme => scheme.ToLowerInvariant())];
  ...
  private static bool IsPrivateOrLoopback(IPAddress address) =>
      address.IsIPv4MappedToIPv6
          ? IsPrivateOrLoopback(address.MapToIPv4())
          : IPAddress.IsLoopback(address) || ...
  ```

- **Suggested fix:** Validate each scheme with
  `ArgumentException.ThrowIfNullOrWhiteSpace` before lowering; extend the
  private-address classifier to unwrap `64:ff9b::/96` (and optionally
  `64:ff9b:1::/48`) before classification.
- **Confidence:** Medium

### A10 — `ProcessEnvironmentVariable` accepts names containing `=`

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Host/ProcessEnvironmentVariable.cs:18`
- **Severity:** Low
- **Category:** validation
- **Description:** The name is checked for blank and NUL only. A name such as
  `"PATH=/tmp"` is accepted; when a process runner materializes `name=value`
  into an environment block, the extra `=` splits the entry so the child sees a
  different variable (`PATH`) with attacker-chosen content. The host boundary
  should reject the malformed name at the value type, since this record is the
  contract every process runner consumes.
- **Evidence:**

  ```csharp
  ArgumentException.ThrowIfNullOrWhiteSpace(name);
  ArgumentNullException.ThrowIfNull(value);
  ArgumentException.ThrowIfContainsNul(name);
  ArgumentException.ThrowIfContainsNul(value);
  ```

- **Suggested fix:** Reject `name.Contains('=')` (and, ideally, leading/trailing
  whitespace) with an `ArgumentException` and document it.
- **Confidence:** High

### A11 — `LlmRequestSettings` equality is non-reflexive for NaN and accepts NaN/±Infinity sampling parameters

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Providers/LlmRequestSettings.cs:166`
- **Severity:** Low
- **Category:** correctness
- **Description:** `Temperature` and `TopP` are unvalidated `double?`.
  `double.NaN` and infinities are accepted and will be forwarded to providers
  (or serialized as invalid JSON numbers). `Equals` uses `==`, so an instance
  whose `Temperature` is `NaN` is not equal to itself while `GetHashCode` is
  stable, breaking reflexivity for dictionary/`Distinct` use and
  `ArgumentException.ThrowIfNotEqual`-style comparisons elsewhere in the
  package.
- **Evidence:**

  ```csharp
  public bool Equals(LlmRequestSettings? other) =>
      other is not null
      && Temperature == other.Temperature
      && TopP == other.TopP
  ```

- **Suggested fix:** Reject non-finite values in the constructor and `init`
  accessors (`double.IsFinite`), and compare with `Nullable.Equals`/`.Equals`
  rather than `==`.
- **Confidence:** High

### A12 — `ModelResponse` allows null `ContentPart` elements

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Providers/ModelResponse.cs:52`
- **Severity:** Low
- **Category:** validation
- **Description:** `parts` is guarded with `ThrowIfDefault` only, whereas every
  message type (`AgentMessage`, `AgentInput`, `ToolResultPart`) requires
  `ThrowIfContainsNull`. A provider adapter that yields a null part produces a
  `ModelResponse` that passes construction, and the failure surfaces later as a
  `NullReferenceException` inside `Parts.SequenceEqual`, hashing, or when the
  loop builds the `AssistantMessage`, far from the offending adapter.
- **Evidence:**

  ```csharp
  ArgumentNullException.ThrowIfNull(identity);
  ArgumentException.ThrowIfDefault(parts);
  ArgumentNullException.ThrowIfNull(usage);
  ```

- **Suggested fix:** Use `ArgumentException.ThrowIfContainsNull(parts)` in the
  constructor and the `Parts` `init` accessor, and update the `<exception>`
  documentation.
- **Confidence:** High

### A13 — `NormalizedHost` surfaces `IdnMapping` failures with a foreign `ParamName`

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Network/NormalizedHost.cs:47`
- **Severity:** Low
- **Category:** validation
- **Description:** `ThrowIfInvalidNetworkHost` uses `Uri.CheckHostName`, which
  accepts `xn--`-prefixed labels without verifying Punycode.
  `new IdnMapping().GetAscii(...)` then throws `ArgumentException` for
  undecodable labels (e.g. `xn--zzzz`) with `ParamName == "unicode"`, so callers
  validating `ParamName` against the constructor's `value` parameter (the
  repository test convention) get a misattributed error and the message is the
  BCL's, not the host contract's.
- **Evidence:**

  ```csharp
  Value = IPAddress.TryParse(candidate, out var address)
      ? address.ToString().ToLowerInvariant()
      : new IdnMapping().GetAscii(candidate).ToLowerInvariant();
  ```

- **Suggested fix:** Wrap `GetAscii` in a `try/catch (ArgumentException)` and
  rethrow an `ArgumentException` ("Value must be a canonicalizable DNS host.")
  with `nameof(value)` as the parameter name and the original as inner, or move
  the IDN check into `ThrowIfInvalidNetworkHost`.
- **Confidence:** Medium

### A14 — `BudgetReservationRequest.Amount` `init` bypasses the positive-amount invariant

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Budgets/BudgetReservationRequest.cs:62`
- **Severity:** Low
- **Category:** validation
- **Description:** The only constructor guard is
  `ThrowIfNegativeOrZero(amount)`, but `Amount` is `{ get; init; }`, so
  `request with { Amount = -5m }` or `{ Amount = 0m }` yields a reservation
  request that the ledger contract treats as invalid;
  `ThrowIfInvalidBudgetLedgerReservationRequest` later rejects it with the
  ledger's parameter name, not at the value's own boundary. Sibling budget types
  (`BudgetLimit`, `BudgetScopeRequest`) validate their `init` accessors.
- **Evidence:**

  ```csharp
  ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
  ScopeId = scopeId;
  ...
  public decimal Amount { get; init; }
  ```

- **Suggested fix:** Give `Amount` a validating `init` accessor mirroring
  `BudgetLimit.Value`.
- **Confidence:** High

### A15 — `MediaReference` equality ignores URI fragments and inline size consistency

- **Package:** AgentKit.Abstractions
- **File:** `src/AgentKit.Abstractions/Messages/Content/MediaReference.cs:83`
- **Severity:** Low
- **Category:** correctness
- **Description:** `Uri == other.Uri` uses `Uri.Equals`, which ignores the
  fragment and is case-insensitive for scheme/host, so two `MediaSourceKind.Uri`
  references differing only in `#fragment` (which some media hosts use for
  range/page selection) compare equal and dedupe into one. Separately, for
  `MediaSourceKind.InlineBytes` the constructor does not require
  `sizeInBytes == inlineBytes.Length` when `sizeInBytes` is supplied, allowing
  self-contradictory evidence.
- **Evidence:**

  ```csharp
  && MediaType == other.MediaType
  && Uri == other.Uri
  && InlineBytes.SequenceEqual(other.InlineBytes)
  ```

- **Suggested fix:** Compare `Uri?.OriginalString` (or `Uri.ToString()` with
  `UriComponents.AbsoluteUri | UriComponents.Fragment`) ordinally, and enforce
  `sizeInBytes == inlineBytes.Length` for inline media.
- **Confidence:** Medium

### A16 — `DelegatingOAuthCredentialSource` propagates a null credential from a misbehaving provider

- **Package:** AgentKit.Abstractions
- **File:**
  `src/AgentKit.Abstractions/Providers/DelegatingOAuthCredentialSource.cs:29`
- **Severity:** Low
- **Category:** validation
- **Description:** `GetCredentialAsync` returns whatever
  `IOAuthAccessTokenProvider.GetAccessTokenAsync` yields without a null check.
  `IProviderCredentialSource` returns a non-nullable `ProviderCredential`; a
  provider returning `null` (or a `ValueTask<OAuthTokenProviderCredential?>` via
  a lenient implementation) leaks a null through the non-nullable contract and
  fails as a `NullReferenceException` inside the branded provider's auth header
  construction rather than as a clear composition error.
- **Evidence:**

  ```csharp
  public async ValueTask<ProviderCredential> GetCredentialAsync(
      ProviderId providerId,
      CancellationToken cancellationToken = default) =>
      await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
  ```

- **Suggested fix:** Capture the result and throw `InvalidOperationException`
  (naming the provider type) when it is null before returning.
- **Confidence:** Medium

---

### Areas reviewed (Group 01)

- `ArgumentExceptionExtensions.cs` (all guards),
  `ArgumentOutOfRangeExceptionExtensions.cs`
- `Identity/`: `AgentId`, `SessionId`, `RunId`, `ToolId`, `ModelId`, `TenantId`,
  `PrincipalId`, `SessionSequence`, `FencingToken`, `ContentHash`,
  `VersionToken`, `IdempotencyKey`, `ToolCallId`, `ComponentId`, `GrantId`,
  `SecurityRequestId`, `ExecutionIdentity`, `DelegationIdentityLink`,
  `IdentityClaim`, `AuthenticationEvidence`, `IIdentifierGenerator`
- `Security/`: `SecurityGrant`, `SecurityRequest`,
  `SecurityCanonicalFingerprint`, `InputFingerprint`, `ProtectedResource`,
  `RedactedAuditValue`
- `Budgets/`: `BudgetQuantity`, `BudgetLimit`, `BudgetSnapshot`,
  `BudgetDimensionUsage`, `BudgetReservationRequest`, `BudgetReservationCursor`
  (partial)
- `Usage/`: `RunUsage`, `RunUsageAggregate`, `UsageAccountingEntry`,
  `UsageAccountingRevision`, `UsageMeasurementQuality`
- `Messages/`: `AgentMessage`, `ModelUsage`; `Messages/Content/`:
  `JsonElementValueEquality`, `StructuredDataPart`, `ToolCallPart`,
  `ToolResultPart`, `ToolReference`, `MediaReference`
- `Providers/`: `SequencingModelResponseObserver`, `ModelResponseEvent`,
  `ModelResponse`, `DenseFloatVector`, `PackedBinaryVector`, `EmbeddingVector`,
  `LlmRequestSettings`, `ModelPricing`, `ProviderFailure`,
  `ApiKeyProviderCredential`, `OAuthTokenProviderCredential`,
  `ProviderCredential`, `StaticApiKeyCredentialSource`,
  `DelegatingOAuthCredentialSource`, `DefaultToolCallIdGenerator`
- `Durability/`: `JsonDurableOperationCodec`, `IDurableOperationCodec`,
  `DurableDecoded`, `OperationPayload`
- `Network/`: `NetworkSecurityBinding`, `NetworkHeaderSet`, `NetworkHeader`,
  `NormalizedHost`, `NetworkDestination`, `NetworkDestinationPolicy`,
  `NetworkMethod`, `NetworkRequest` (equality/doc)
- `Host/`: `FileSecurityBinding`, `FileSystemPath`, `FileSearchPattern`,
  `GlobPattern`, `FileSearchPatternKind`, `ProcessEnvironmentVariable`,
  `ProcessResourceLimits`
- `Tools/`: `ToolCallResult`, `ToolTerminalStatus(+Extensions)`, `ToolAlias`,
  `ToolIdentity`, `ToolEffects`, `ToolSchemaLimits`, `ToolResultBounds`,
  `ToolResultProjectionTransformations` (+ usage sites)
- `Sessions/`: `SessionOperationContext`, `SessionEntryWireEnvelope`;
  `Composition/`: `OperationCorrelation` family, `ComponentKey`,
  `ConfigurationJsonValue`, `EffectiveConfigurationSnapshot` (partial),
  `AgentDefinition` (equality), `AgentCatalogSnapshot` (partial)
- `Extensibility/`: `ExtensionData`, `ExtensionValue`;
  `Hooks/HookDispatchScope`; `Schemas/JsonSchema`; `Input/AgentInput`,
  `Input/AdmittedInput`; `Loop/AgentRunRequest`; `Artifacts/ArtifactReadOpened`
- Package-wide greps: ambient clocks/`Guid.NewGuid`, mutable statics, `catch`,
  `lock`/`.Result`/`.Wait()`, `Enum.IsDefined` on `[Flags]`,
  `StringComparison`/`ToLower`/`ToUpper`/`CultureInfo`, arithmetic operators,
  async signatures without `CancellationToken` (none found), records with
  `ImmutableArray` lacking custom equality (list enumerated), records with
  validated ctors but bare `{ get; init; }`.
- Tests consulted: `JsonDurableOperationCodecTests`, `SecurityGrantTests`,
  `SequencingModelResponseObserverTests` (test names), test directory layout.

### Areas not reviewed (Group 01)

- `Compaction/`, `Context/`, `Deferral/`, `Delegation/`, `Language/`, `Output/`,
  `Planning/`, `Results/`, `WebSearch/`, `Foundation/`, `Hooks/` (beyond
  `HookDispatchScope`) — only skimmed via greps, not read line by line.
- Most of `Sessions/` (≈190 files), `Budgets/` ledger result/exception types,
  `Tools/` catalog/merge/presentation records, `Providers/` embedding
  request/response, model catalog/selection records, capability validation
  results.
- Remaining `*SecurityBinding.cs` files (`Process`, `Directory`, `Glob`,
  `FileSearch`, `WorkspacePatch`, `Language`, `WebSearch`, `Artifact`, `Plan`,
  `HumanQuestion`, `TaskDelegation`) beyond the shared `Hash` pattern.
- `ArgumentExceptionExtensions.cs` beyond line ~1630 (file continues) and
  `ServiceExtensions.cs`, `AssemblyInfo.cs`, `generated/`.
- The `~45` records listed under A05 were identified mechanically; only
  `NetworkRequest`, `AgentInput`, `AgentCatalogSnapshot`, and the loop/session
  ones were inspected for documentation claims or equality usage.

## Group 02 — Session, Session.InMemory, Session.Sqlite

Read-only review of `src/AgentKit.Session`, `src/AgentKit.Session.InMemory`, and
`src/AgentKit.Session.Sqlite` against the `ISessionStore` / `ISessionDirectory`
/ `ISessionRunCoordinator` contracts and the shared conformance suite. The two
store adapters were compared method-by-method; their validation order, typed
results, sequence/version semantics, idempotency scoping, tenant masking,
lane-cursor advancement, fork copying, and paging/snapshot logic are consistent,
and the SQLite adapter's `BEGIN IMMEDIATE` per-operation transaction makes every
read-decide-write sequence atomic with its commit. No High-severity defect was
found. Findings: **0 High, 2 Medium, 4 Low**. The Medium items are (a) neither
adapter validates that appended entries actually belong to the addressed
session/branch, and (b) the run coordinator's durable lane release reads the
whole-session version in one transaction and CASes on it in another with no
retry, so any concurrent mutation on another lane leaves the durable lane
permanently busy.

### S01 — Appended entries' `Address`/`BranchId` are never validated against the request

- **Package:** AgentKit.Session.Sqlite
- **File:** `src/AgentKit.Session.Sqlite/SqliteSessionStore.cs:283`
- **Severity:** Medium
- **Category:** validation
- **Description:** `AppendCoreAsync` validates `Sequence` contiguity, entry-id
  and message-id uniqueness, and codec encodability, but never checks that each
  `SessionEntry.Address` equals the addressed session or that
  `SessionEntry.BranchId` equals `request.BranchId`. `SessionAppendRequest` does
  not validate this either. A caller can therefore commit an entry whose
  self-declared identity names a different session or a sibling branch; the row
  is stored under branch Y while its decoded payload says branch X (SQLite
  stores the payload verbatim, InMemory stores the object as-is), and every
  later reader that trusts `entry.BranchId`/`entry.Address` (history projection,
  compaction, export) sees inconsistent provenance. The in-memory adapter has
  the identical gap at
  `src/AgentKit.Session.InMemory/InMemorySessionStore.cs:336`. Note that
  fork-copied entries intentionally retain the parent's `BranchId` (documented),
  so the check belongs on _appended_ entries only.
- **Evidence:**

```csharp
for (var i = 0; i < request.Entries.Length; i++)
{
    var expectedSequence = tip.Value.TipSequence + i + 1;
    if (request.Entries[i].Sequence.Value != expectedSequence)
    {
        return new SessionAppendFailed(
            $"Entry at position {i} has sequence {request.Entries[i].Sequence.Value}; expected {expectedSequence}.");
    }
}
```

- **Suggested fix:** In both adapters (or in `SessionAppendRequest`'s
  constructor, which already has `context` and `branchId`), reject with
  `SessionAppendFailed` any entry whose `Address != context.ToAddress()` or
  `BranchId != request.BranchId`; add a conformance test for each.
- **Confidence:** High
- **Status:** Fixed ✅

### S02 — Durable lane release CASes on a session version read in a separate transaction and never retries

- **Package:** AgentKit.Session
- **File:** `src/AgentKit.Session/DefaultSessionRunCoordinator.cs:222`
- **Severity:** Medium
- **Category:** concurrency
- **Description:** `ReleaseDurableStateAsync` calls `LoadAsync` to obtain
  `Descriptor.Version`, then issues
  `SessionRunReleaseRequest(..., ExpectedVersion: thatVersion)`.
  `SessionVersion` is the whole-session token and advances on every mutation of
  any branch/lane, so an append, admission, or branch on _another_ lane between
  the two calls makes the store return
  `SessionRunReleaseRejected(SessionVersion)`. The rejection is logged and
  swallowed, the local slot is released by `DisposeAsync`, but the lane's
  durable `accepted_state` stays installed, so every later `AcceptRunAsync` on
  that lane returns `SessionRunStartBusy` indefinitely (no TTL or recovery
  exists in this package). Multi-lane sessions are the intended use of lanes, so
  this TOCTOU window is hit under ordinary load rather than only in pathological
  cases. The XML remark documents the swallow, not the stale-version race.
- **Evidence:**

```csharp
var loaded = await session.Coordinator.LoadAsync(context, session.Profile, cancellationToken)
    .ConfigureAwait(false);
...
var release = new SessionRunReleaseRequest(
    context, expectedStateRevision, sessionLoaded.Descriptor.Version, idempotencyKey);
var result = await session.Coordinator.ReleaseRunAsync(release, session, cancellationToken)
    .ConfigureAwait(false);
```

- **Suggested fix:** Retry load+release a bounded number of times when the
  rejection kind is `SessionVersion` (or let the store's release accept a
  lane-revision/state-revision fence instead of the whole-session version, which
  the release already validates via `ExpectedStateRevision`).
- **Confidence:** Medium
- **Status:** Fixed ✅

### S03 — Lane slots are never removed from `_slots`, so the coordinator grows without bound

- **Package:** AgentKit.Session
- **File:** `src/AgentKit.Session/DefaultSessionRunCoordinator.cs:77`
- **Severity:** Low
- **Category:** resource-leak
- **Description:** `AcquireAsync` does `_slots.GetOrAdd(key, ...)` for every
  `(tenant, address, lane)` ever acquired, and `Release` only clears
  `slot.Owner`; nothing ever calls `TryRemove`. In a long-lived host each new
  session/lane leaks a `SessionRunSlot` (a `SemaphoreSlim` plus monitor object)
  for the process lifetime, including for sessions that were later deleted.
- **Evidence:**

```csharp
var slot = _slots.GetOrAdd(key, static _ => new SessionRunSlot());
...
lock (slot.SyncRoot)
{
    if (slot.Owner?.LeaseId != leaseId) { return; }
    slot.Owner = null;
    _ = slot.Gate.Release();
}
```

- **Suggested fix:** On release, remove the slot when it has no owner and no
  waiters (e.g., track a waiter count under `SyncRoot` and `TryRemove` with the
  same instance), or bound the dictionary with an eviction policy for idle
  slots.
- **Confidence:** High
- **Status:** Fixed ✅

### S04 — `Outcome` classifier omits release and directory-list results, so successful releases/lists are recorded as failed/"unknown"

- **Package:** AgentKit.Session
- **File:** `src/AgentKit.Session/DefaultSessionCoordinator.cs:677`
- **Severity:** Low
- **Category:** correctness
- **Description:** `ObserveAsync` sets the activity status to success only when
  `Outcome(result) == "success"`, otherwise `SetFailed(outcome, outcome)`, and
  records the metric with that outcome. `Outcome` does not list
  `SessionRunReleased`, `SessionRunReleaseRejected`, `SessionDirectoryPage`, or
  `SessionDirectoryListUnavailable`, so every `ReleaseRunAsync` and `ListAsync`
  (both routed through `ObserveAsync`) emits status Error with outcome
  `"unknown"` even when it succeeded. This violates the "terminal activity set
  to a truthful success or error status" invariant and pollutes the bounded
  `Outcome` metric dimension with a value that hides real failures.
- **Evidence:**

```csharp
SessionCreated or SessionLoaded or SessionAppended or SessionPage or SessionBranched or SessionDeleted or
    SessionInputReplayFound or SessionInputNotFound or SessionExecutionLaneProvisioned or AcceptedInput or
    SessionRunAccepted or SessionRunStateLoaded =>
    "success",
...
_ => "unknown",
```

- **Suggested fix:** Add `SessionRunReleased` and `SessionDirectoryPage` to the
  success arm and `SessionRunReleaseRejected`/`SessionDirectoryListUnavailable`
  to the failed arm; add tests asserting the activity status for release and
  list.
- **Confidence:** High
- **Status:** Fixed ✅

### S05 — Directory listing loads every route for the tenant/agent per page; `MaximumResults` and `AfterSessionId` are not pushed into SQL

- **Package:** AgentKit.Session.Sqlite
- **File:**
  `src/AgentKit.Session.Sqlite/SqliteSessionDirectoryUnitOfWork.cs:172`
- **Severity:** Low
- **Category:** correctness
- **Description:** `ListCandidateLocationsAsync` selects all rows for
  `(tenant_id, agent_id)` and `SqliteSessionDirectory.ListCoreAsync` filters by
  owner, applies the `AfterSessionId` cursor, sorts, and takes
  `MaximumResults + 1` in memory. Each page is therefore O(total routes for the
  agent) in I/O and allocations, so the paging contract does not actually bound
  work in the durable adapter; a tenant with many sessions makes every page (and
  every continuation) a full scan. Ordering is done with `Guid.CompareTo`
  in-process, which is at least self-consistent with the cursor comparison, so
  no rows are skipped or duplicated.
- **Evidence:**

```csharp
SELECT session_id, store_key, directory_revision, recorded_at, schema_version, owner_principal_id
FROM {SqliteSessionDirectorySchema.LocationsTable} WHERE tenant_id = $tenant AND agent_id = $agent;
...
var ordered = candidates
    .Where(candidate => candidate.Owner == scan.Identity.PrincipalId
        && (scan.AfterSessionId is null
            || candidate.Location.Address.SessionId.Value.CompareTo(scan.AfterSessionId.Value.Value) > 0))
    .OrderBy(static location => location.Address.SessionId.Value)
    .Take(scan.MaximumResults + 1)
```

- **Suggested fix:** Add `owner_principal_id = $owner`, an
  `ORDER BY`/`LIMIT $max+1`, and a cursor predicate to the SQL. Because
  `Guid.CompareTo` differs from lexicographic ordering of the `"D"` text, either
  store a sortable key (e.g., a 16-byte BLOB in `Guid.CompareTo` order) or
  switch the cursor comparison to ordinal string order so SQL and in-process
  ordering agree.
- **Confidence:** High
- **Status:** Fixed ✅

### S06 — Corrupt persisted value objects surface as `TargetInvocationException` instead of `JsonException`

- **Package:** AgentKit.Session.Sqlite
- **File:** `src/AgentKit.Session.Sqlite/SqliteValueObjectJsonConverter.cs:24`
- **Severity:** Low
- **Category:** serialization
- **Description:** `Read` rebuilds single-property structs via
  `ConstructorInfo.Invoke`. When a persisted value fails the struct's validating
  constructor (e.g., a default `Guid`, a non-positive revision) the
  `ArgumentException` is wrapped in `TargetInvocationException`, which is not a
  `JsonException`, so it escapes the store as an unrelated reflection exception
  rather than the malformed-payload failure that
  `ReadEntriesAsync`/`Deserialize<T>` document
  (`<exception cref="JsonException">`). Callers and the diagnostics `error.type`
  tag see `TargetInvocationException`, obscuring that a receipt/admission/lane
  row is corrupt.
- **Evidence:**

```csharp
var value = element.Deserialize(_property.PropertyType, options)
    ?? throw new JsonException($"The {_property.Name} value for {typeof(T).Name} is null.");
return (T) _constructor.Invoke([value]);
```

- **Suggested fix:** Catch `TargetInvocationException` (and `ArgumentException`)
  around `Invoke` and rethrow as `JsonException` with a content-free message.
- **Confidence:** Medium
- **Status:** Fixed ✅

### Areas reviewed (Group 02)

- `SqliteSessionStore` (all `*CoreAsync` operations, snapshot retention,
  `CanPersist`, lane-cursor advancement), `SqliteSessionStore.Observability`
  (grant enforcement, read/write routing), `SqliteSessionUnitOfWork` (+
  `Admissions`, `Lanes`, `Idempotency` partials): every SQL statement, parameter
  typing (GUID/timestamps as text are only equality-compared;
  `sequence`/`version`/`revision` are INTEGER), `ORDER BY`/`LIMIT` on the paged
  read, `hasMore`/`throughSequence` boundary behaviour, fork copy and tip
  update, cascade delete order vs. foreign keys, create/deleted-create/delete
  receipt migration.
- `SqliteSessionDatabase` / `SqliteSessionDirectoryDatabase`: deferred vs.
  `BEGIN IMMEDIATE` transactions, commit/rollback on exception and on
  cancellation, busy timeout wiring, bootstrap/validation modes, schema-version
  and instance-id checks, connection/command/reader disposal.
- `SqliteSessionSchema`, `SqliteSessionDirectorySchema`,
  `SqliteSessionStoreTarget`, `SqliteSessionStoreSettings/Options`,
  `ServiceExtensions` (both adapters), JSON converter factories and type
  resolver.
- `InMemorySessionStore` (+ `Observability`),
  `SessionRecord`/`BranchRecord`/`StoredAdmission`, `InMemorySessionDirectory`:
  lock scope, mutable-state escape, method-by-method parity with the SQLite
  adapter (ordering of checks, result types, idempotency scoping, tenant
  masking).
- `SqliteSessionDirectory` vs `InMemorySessionDirectory`:
  record/record-create/locate/list semantics and paging.
- `AgentKit.Session`: `DefaultSessionCoordinator` (routing, grant issuance,
  create flow convergence, event publication), `DefaultSessionRunCoordinator` +
  `SessionRunLease`/`SessionRunSlot` (gate/owner linearization, busy-wait,
  cancellation, durable release), `DefaultSessionStoreSelector`,
  `SessionStoreBindingFactory`, `SessionEntryCodecCatalog`,
  `BoundedWriteStream`, options validation.
- Ambient-clock and `Guid.NewGuid` usage (only inside the replaceable
  `GuidIdentifierGenerator` defaults; all timestamps come from `TimeProvider`).
- Conformance and adapter test inventories (`SessionStoreConformanceTests`,
  `SqliteSessionStoreTests`, `SqliteSessionUnitOfWorkTests`) to confirm intended
  behaviour before flagging.

### Areas not reviewed (Group 02)

- Byte-level correctness of the portable entry codecs
  (`PortableSessionEntryJson`, `PortableSessionSecurityJson`,
  `PortableSessionJsonPolymorphism`, the six `*SessionEntryCodec` classes)
  beyond their exception-handling shape; they have dedicated round-trip test
  suites.
- Reflection-based JSON round-trip fidelity of every request/result record
  persisted in idempotency receipts, admissions, and lane accepted state
  (covered by the SQLite reopen/replay tests but not re-derived here).
- Observability event IDs/templates in `SessionLog`, `SessionStoreLog`,
  `SessionDirectoryLog`, and metric definitions.
- `SessionEntryCodecObservation`, `NeverRetireSessionRetentionPolicy`, and the
  `AgentKit.Abstractions` request/result types themselves except where their
  validation affected an adapter finding.
- Behaviour under multiple `SqliteSessionStore` instances sharing one database
  file (documented as unsupported for exact continuation snapshots) and the
  absence of a directory delete/tombstone after session deletion (a
  contract-level design question rather than an adapter defect).

## Group 03 — Loop, Conversations, IO, Output

Read-only review of `src/AgentKit.Loop`, `src/AgentKit.Conversations`,
`src/AgentKit.IO`, and `src/AgentKit.Output` (~10.5k lines) against the mirror
tests and the invariants in `AGENTS.md`. The loop's state machine, turn
accounting, cancellation-vs-typed-outcome handling, tool batch settlement, and
settlement-bounded commits are largely sound and well covered by tests; the IO
hub/subscription lock ordering and wakeup logic hold up under inspection; the
Output pipeline enforces bounds before parsing and clones `JsonElement`s out of
their documents. The defects found are concentrated in the Conversations wrapper
(non-deterministic idempotency keys, synchronous scope disposal, outcome
mislabelling) plus a few narrow edge cases in the loop, publisher, and output
deserialization. Counts: **High 0, Medium 2, Low 5** (7 total).

---

### L01 — Conversation session mints idempotency keys with `Guid.NewGuid()`, bypassing injected identity and defeating idempotency

- **Package:** AgentKit.Conversations
- **File:** `src/AgentKit.Conversations/DefaultConversationSession.cs:508` (also
  `:592`)
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** Both the session-create request and the user-message append
  use `new IdempotencyKey(Guid.NewGuid().ToString())`. The class already injects
  `IIdentifierGenerator<RunId>`, `IIdentifierGenerator<OperationId>`, etc., and
  the repository invariant says deterministic creation uses an injected
  generator and that ambient `Guid.NewGuid` is not used where a generator
  exists. Functionally, a fresh random key per attempt means the coordinator's
  idempotency protection can never de-duplicate a retried admission (a host
  retrying `SendAsync` after a transport fault appends the same user text
  twice), and tests cannot make the key deterministic. The loop itself derives
  keys from stable identity (`run:{RunId}:turn:{TurnId}:assistant`), so the
  wrapper is inconsistent with the component it drives.
- **Evidence:**

  ````csharp
  var appendResult = await _sessionCoordinator.AppendAsync(
      new SessionAppendRequest(
          new SessionOperationContext(_agentId, _sessionId.Value, null, correlation, _identity, appendAuthorization),
          _branchId,
          currentVersion,
          new IdempotencyKey(Guid.NewGuid().ToString()),
          [userMessage]),
  ```text
  ```csharp
  null,
  new IdempotencyKey(Guid.NewGuid().ToString()),
  ExtensionData.Empty),
  ````

- **Suggested fix:** Derive the keys from already-generated identities (e.g.
  `$"conversation:{runId}:user"` for the append and a value from an injected
  generator/`OperationId` for session creation) so a retry with the same `RunId`
  is idempotent and tests can control the value.
- **Confidence:** High
- **Status:** Fixed ✅

### L02 — Loop scope is disposed synchronously; an `IAsyncDisposable`-only scoped loop throws after the turn already committed

- **Package:** AgentKit.Conversations
- **File:** `src/AgentKit.Conversations/DefaultConversationSession.cs:562`
- **Severity:** Medium
- **Category:** async
- **Description:** `SendCoreAsync` resolves the keyed `IAgentLoop` from
  `_loopScopeFactory.CreateScope()` and disposes it with a synchronous `using`.
  `IAgentLoop` is registered **scoped** and is an explicitly replaceable
  extension point (`AddAgentLoop<TLoop>` / `ReplaceAgentLoop<TLoop>`). If the
  host's loop (or any scoped dependency it pulls in) implements only
  `IAsyncDisposable`, Microsoft DI's `ServiceProviderEngineScope.Dispose()`
  throws `InvalidOperationException` ("...implements IAsyncDisposable but not
  IDisposable"). That exception fires after `RunAsync` returned, so a fully
  committed turn is reported to the caller as a fault and its
  `ConversationTurnResult` is lost. `AgentEngine`
  (src/AgentKit/AgentEngine.cs:289) already uses `CreateAsyncScope()` for the
  same pattern.
- **Evidence:**

  ```csharp
  using var loopScope = _loopScopeFactory.CreateScope();
  var agentLoop = loopScope.ServiceProvider.GetRequiredKeyedService<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue);
  var loopResult = await agentLoop.RunAsync(request, _runServices, cancellationToken).ConfigureAwait(false);
  ```

- **Suggested fix:** Use
  `await using var loopScope = _loopScopeFactory.CreateAsyncScope();` (with
  `ConfigureAwait(false)`), matching `AgentEngine`.
- **Confidence:** Medium
- **Status:** Fixed ✅

### L03 — `CompleteAsync`/`DisposeAsync` race leaves a retained final result whose `Completion` is cancelled

- **Package:** AgentKit.IO
- **File:** `src/AgentKit.IO/AgentRunOutputPublisher.cs:127`
- **Severity:** Low
- **Category:** concurrency
- **Description:** `CompleteAsync` stores `_finalResult` under `_gate`, releases
  the lock, seals the hub, and only then calls
  `_completion.TrySetResult(result)`. `DisposeAsync` calls
  `_completion.TrySetCanceled()` without touching `_gate`. If disposal
  interleaves between the lock release and `TrySetResult`, the producer's
  `CompleteAsync` returns normally (it believes the envelope was exposed and any
  later identical `CompleteAsync` is silently treated as a duplicate success),
  yet every subscriber's `Completion` faults with cancellation. The remarks
  promise disposal "never changes a settlement that already happened", which
  this window violates.
- **Evidence:**

  ````csharp
      _outputType = typeof(TOutput);
      _finalResult = result;
  }

  // Sealing the event stream before exposing the envelope ...
  _hub.Complete();
  _ = _completion.TrySetResult(result);
  ```text
  ```csharp
  public ValueTask DisposeAsync()
  {
      _ = _completion.TrySetCanceled();
      return _hub.DisposeAsync();
  }
  ````

- **Suggested fix:** Perform `TrySetResult` inside the same `_gate` critical
  section that records `_finalResult` (hub sealing can follow), and have
  `DisposeAsync` take `_gate` and skip `TrySetCanceled` when `_finalResult` is
  already set.
- **Confidence:** Medium
- **Status:** Fixed ✅

### L04 — Non-completed run outcomes are logged/metered as "admission failed"

- **Package:** AgentKit.Conversations
- **File:** `src/AgentKit.Conversations/DefaultConversationSession.cs:571` (also
  `:405`)
- **Severity:** Low
- **Category:** correctness
- **Description:** After the user message was successfully admitted and the loop
  ran, any outcome other than `AgentRunCompleted` (typed cancellation after a
  commit, provider failure, turn limit, output rejection, invalid state) emits
  `ConversationLog.TurnAdmissionFailed` (event 24002, Warning, "could not admit
  the user's message") and `SendObservedAsync` records the terminal metric and
  `ConversationTurnCompletedEvent` outcome as `"admission_failed"`. The message
  was admitted; the run settled unsuccessfully. This contradicts the
  observability invariant that severity/outcome reflect the semantic result, and
  it makes a post-commit cancellation indistinguishable in metrics from a
  rejected append (the pre-commit OCE path correctly reports `"cancelled"`).
- **Evidence:**

  ````csharp
  var loopResult = await agentLoop.RunAsync(request, _runServices, cancellationToken).ConfigureAwait(false);
  var events = ProjectEvents(loopResult);
  if (loopResult.Outcome is AgentRunCompleted)
  {
      return new ConversationTurnResult(true, events);
  }

  ConversationLog.TurnAdmissionFailed(_logger, _agentId);
  ```text
  ```csharp
  var outcome = result.Succeeded ? "settled" : "admission_failed";
  ````

- **Suggested fix:** Add a distinct log event (e.g. `TurnRunNotCompleted`
  carrying the outcome type name) and thread a bounded outcome string
  (`"cancelled"`, `"failed"`, `"turn_limit"`, ...) through
  `ConversationTurnResult` or a side channel so the metric/terminal event
  reflects the real outcome.
- **Confidence:** High
- **Status:** Fixed ✅

### L05 — Runtime-type deserialization only catches `JsonException`; unsupported runtime types escape as run faults

- **Package:** AgentKit.Output
- **File:** `src/AgentKit.Output/DefaultOutputProcessor.cs:249`
- **Severity:** Low
- **Category:** correctness
- **Description:** `System.Text.Json` reports configuration-class problems with
  `NotSupportedException` (interface/abstract `RuntimeType`, types without a
  usable constructor, unsupported collections) and `InvalidOperationException`
  (property-name collisions under `PropertyNameCaseInsensitive = true`, invalid
  converters). Only `JsonException` is caught, so these propagate out of
  `ProcessAsync` as exceptions instead of a typed `OutputConfigurationRejected`,
  and neither `InMemoryOutputDefinitionRegistry` nor `PreflightDefinition`
  validates `RuntimeType`. Per the structured-output concept, invalid
  definitions are configuration failures that must not spend repair attempts or
  crash the run; today they surface as an unhandled fault mid-run (after the
  model call already happened).
- **Evidence:**

  ```csharp
  try
  {
      deserialized = JsonSerializer.Deserialize(
          JsonSerializer.SerializeToUtf8Bytes(extraction.Json.Value, _deserializationOptions),
          definition.RuntimeType,
          _deserializationOptions);
  }
  catch (JsonException exception)
  {
  ```

- **Suggested fix:** Catch `NotSupportedException`/`InvalidOperationException`
  here and return `OutputConfigurationRejected` (distinct from the
  model-attributable `DeserializationFailed` retry path), and/or preflight
  `RuntimeType` at registration via `JsonSerializerOptions.GetTypeInfo`.
- **Confidence:** Medium
- **Status:** Fixed ✅

### L06 — Zero-effect caller cancellation is surfaced two different ways depending on adapter behaviour

- **Package:** AgentKit.Loop
- **File:** `src/AgentKit.Loop/DefaultAgentLoop.cs:614`
- **Severity:** Low
- **Category:** contract-mismatch
- **Description:** The class remarks (lines 58–59) state that caller
  cancellation propagates as `OperationCanceledException` while the run has
  committed nothing, and the `RunCoreAsync` catch (line 355) enforces that by
  only converting to `AgentRunCancelled` when `committedMessages.Count > 0`. But
  when the adapter honours the same caller token by _returning_
  `ModelAttemptCancelled` with no partial parts, `SettleInterruptedAsync`
  returns `TurnOutcome.Settled(AgentRunCancelled)` immediately (line 1597–1600),
  so the run settles with a typed outcome despite zero durable effects. The same
  user cancellation therefore throws for one adapter and returns for another;
  downstream, `DefaultConversationSession` treats the throw as `"cancelled"` and
  the typed result as a non-succeeded turn labelled `"admission_failed"` (see
  L04).
- **Evidence:**

  ````csharp
  ModelAttemptCancelled cancelled => await SettleInterruptedAsync(
      request, services, model, history.SourceCursor, turnSessionContext, turnCorrelation, turnId, modelRequestId,
      cancelled.PartialParts, cancelled.Usage, NormalizedStopReason.Cancelled,
      cancelled.Cancellation.RequestId, new AgentRunCancelled(cancelled.Cancellation.SafeMessage),
      committedMessages, currentVersion)
  ```text
  ```csharp
  if (partialParts.IsEmpty)
  {
      return TurnOutcome.Settled(outcome, currentVersion);
  }
  ````

- **Suggested fix:** In the `ModelAttemptCancelled` arm, when
  `cancellationToken.IsCancellationRequested`, `committedMessages.Count == 0`,
  and `cancelled.PartialParts.IsEmpty` all hold, throw
  `new OperationCanceledException(cancellationToken)` to match the documented
  zero-effect contract (or, if the typed outcome is preferred, update the
  remarks and make the `RunCoreAsync` catch unconditional).
- **Confidence:** Medium

### L07 — Dangling-tool-call recovery derives its idempotency key and causal parent from the wrong assistant message

- **Package:** AgentKit.Loop
- **File:** `src/AgentKit.Loop/DefaultAgentLoop.cs:1722`
- **Severity:** Low
- **Category:** correctness
- **Description:** While scanning history, `danglingEntry` is overwritten by
  _every_ complete `AssistantMessage` that contains a `ToolCallPart`, regardless
  of whether that message's calls were later resolved. If an earlier assistant
  message left call `c1` unresolved and a later assistant message requested `c2`
  which _was_ resolved (possible via imported/branched history or a different
  writer), `pendingCalls = {c1}` but `danglingEntry` is the later message. The
  recovery entry then uses `recovery:dangling-tools:{laterMessageId}` as its
  idempotency key and the later entry as `causalParentId`, so the settlement is
  attributed to a message that has no dangling call, and a second recovery keyed
  on the correct earlier message would not be de-duplicated.
- **Evidence:**

  ```csharp
  case ToolCallPart call when messageEntry.Message is AssistantMessage:
      _ = pendingCalls.TryAdd(call.CallId, call);
      danglingEntry = messageEntry;
      break;
  case ToolResultPart result when messageEntry.Message is ToolMessage:
      _ = pendingCalls.Remove(result.CallId);
      break;
  ```

- **Suggested fix:** Track the owning entry per call (e.g.
  `Dictionary<ToolCallId, (ToolCallPart Call, MessageSessionEntry Owner)>`) and,
  after the scan, pick the owner of the earliest still-pending call (or emit one
  recovery entry per owning message) for both the idempotency key and the causal
  parent.
- **Confidence:** Medium

---

### Areas reviewed (Group 03)

- `AgentKit.Loop`: `DefaultAgentLoop` end-to-end (run/turn state flow,
  `MaxTurns` accounting, final-turn tool disabling, stop-reason mapping,
  `ModelAttemptFailed/Cancelled/Completed` settlement, sequential tool batch
  invocation and per-call terminal results, interruption/skip handling,
  settlement-bounded commits with `TimeProvider`-driven backoff, append-conflict
  rebase and stale-response refusal, dangling-call recovery, compaction-aware
  history load, authorization capture/mismatch, observer isolation and detached
  delivery timeout, activity/metric emission); `DefaultRunContinuationPolicy`;
  `AgentLoopRegistration`/`AgentLoopOptions`; `RunModelResponseObserver`;
  `GuidIdentifierGenerator`.
- `AgentKit.IO`: `RunEventHub` (subscribe bound, ordered publish, lock ordering
  hub→subscription, complete/dispose), `RunEventSubscription` (bounded queue,
  slow-consumer disconnect, TCS wakeup/reset logic, single-enumeration guard,
  cancellation and release), `RunEventStream`, `AgentRunOutputPublisher` (type
  binding, single-winner completion), `DefaultInputCoordinator`,
  `DefaultInputPromotionPolicy` (steer vs follow-up selection order checked
  against `docs/concepts/input-admission-and-message-queues.md`),
  `DefaultHumanQuestionBroker`, `HumanQuestionEnforcementReceipt`,
  `ServiceExtensions`, options records.
- `AgentKit.Output`: `DefaultOutputProcessor` (preflight order, mode gating,
  text/JSON extraction and UTF-8 bounding, `JsonDocument` clone lifetime,
  structural/duplicate-member checks, schema evaluation, deserialization
  options, validator ordering and issue capping, retry-attempt arithmetic vs
  `OutputRetryPolicy` semantics), `StructuralOutputSchemaEngine` (keyword
  whitelist, dialect handling, exact-integer check incl. exponent saturation,
  depth/node/byte bounds, manifest revalidation), `BoundedJsonSerializer`,
  `BoundedHashStream`, `InMemoryOutputDefinitionRegistry`, options snapshot
  validation.
- `AgentKit.Conversations`: `DefaultConversationSession`
  (Send/Open/List/ReadHistory, turn lock, session creation, admission append,
  loop scope, event projection, disposal), `OwnedConversationSession`,
  `AsyncOwnedConversationSession`, `ConversationRunObserver`,
  `ConversationToolPresentationProjector`, default interface members on
  `IConversationSession`, `ServiceExtensions`.
- Grep sweeps across all four packages for `UtcNow`, `Guid.NewGuid`,
  `Task.Delay`, `.Result`/`.Wait()`, `ConfigureAwait`, `Channel`,
  `Interlocked`/`Volatile`, `catch (`, `OperationCanceledException`,
  `CancellationTokenSource`, `JsonDocument`/`JsonSerializer`, `Dispose`.

### Areas not reviewed (Group 03)

- `CompactionCheckpointProjector` / `CompactionCheckpointProjection` internals
  beyond their call sites in the loop.
- `LoopLog`, `IOLog`, `OutputLog`, `ConversationLog` templates and event-ID
  uniqueness (only spot-checked where a finding referenced them).
- `RunEventHubObservation`, `IOMetrics`, `LoopMetrics`, `OutputMetrics`,
  `ConversationMetrics` tag cardinality.
- `ConversationSessionOptions` validation details and the `Conversation*Event`
  value types.
- Contracts in `src/AgentKit.Abstractions` were consulted only as needed (e.g.
  `AgentRunRequest`, `LlmModelRequest.Deadline`, `OutputRetryPolicy`,
  `OutputProcessingRequest`, `OutputValidationPolicy`, `StructuredDataPart`); no
  review of their own invariants (e.g. `AgentRunRequest.MaxTurns` `init`
  bypassing the constructor guard is out of scope here).
- Provider-side enforcement of `AttemptTimeout`/`Deadline` (verified only that
  `OpenAICompatibleLlmModelBase` distinguishes deadline timeout from caller
  cancellation; other adapters not inspected).
- Live behaviour under real hosts/examples; no code was executed.

## Group 04 — Tools runtime and feature tools

Read-only review of `src/AgentKit.Tools/` and the seventeen
`AgentKit.Tools.<Name>` feature packages against `AGENTS.md` ("Tools and
security"), the agentkit-tools skill, and the mirror tests. The bounded JSON
Schema 2020-12 subset (exact decimal numbers, rune-counted lengths,
duplicate-key rejection, unsupported-keyword rejection, work/depth/node/byte
charging) and the capture/lease/discovery ownership machinery held up well under
scrutiny; no ReDoS, ambient clock, unbounded parser, or double-dispose defects
were found there. The defects found are concentrated in (a) the invocation path
never exercising the schema engine, (b) a coherence gap in merge-policy alias
validation, (c) patch hunk anchoring, and (d) outcome/effect misreporting in a
few feature tools. Totals: **0 High, 4 Medium, 4 Low** (8 findings).

### T01 — Tool arguments are never validated against the compiled schema before invocation

- **Package:** AgentKit.Tools
- **File:** `src/AgentKit.Tools/DefaultToolInvoker.cs:145`
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** `AGENTS.md` requires that "every call passes schema
  validation and the configured security authority before invocation", and the
  package ships `BoundedToolSchemaEngine`/`CompiledToolSchema` for exactly that.
  However `DefaultToolInvoker.InvokeAsync` resolves, authorizes, and then hands
  `request.Arguments` straight to `ITool.InvokeAsync`; nothing in `src/` (Tools,
  Loop, Conversations, AgentKit facade) ever calls `IToolSchemaEngine.Compile`
  or `ICompiledToolSchema.Validate` on a live call. Every feature tool therefore
  relies solely on its own ad-hoc `TryParse`, and declared schema constraints
  that a tool does not re-implement are silently unenforced (e.g. `CommandTool`
  declares `"additionalProperties": false` but its `TryParse` accepts arbitrary
  extra members; `ReadFileTool` declares `"minimum": 1` but only enforces it in
  code). Third-party `ITool` implementations get no canonical validation at all.
- **Evidence:**

```csharp
var invocationRequest = new ToolInvocationRequest(request.Context, request.Arguments, request.RequestedAt);

try
{
    var result = await tool.InvokeAsync(invocationRequest, cancellationToken).ConfigureAwait(false);
```

```text
$ rg -n "IToolSchemaEngine|ICompiledToolSchema" src --glob '*.cs' -l
src/AgentKit.Tools/ServiceExtensions.cs        # registration only
src/AgentKit.Tools/{BoundedToolSchemaEngine,CompiledToolSchema,ToolSchemaProcessor}.cs
src/AgentKit.Abstractions/Tools/...
```

- **Suggested fix:** Compile each descriptor's `InputSchema` once (at catalog
  capture/composition) and have the invoker (or the executor that owns it) run
  `Validate` before authorization, mapping `Invalid` to
  `ToolTerminalStatus.InvalidArguments` and `ResourceLimitExceeded` to a
  distinct resource-exhaustion status.
- **Confidence:** High

### T02 — Merge-graph `Apply` lets a policy bind an authored alias to a tool it was never authored for

- **Package:** AgentKit.Tools
- **File:** `src/AgentKit.Tools/ToolCatalogMergeGraph.cs:125`
- **Severity:** Medium
- **Category:** validation
- **Description:** `Apply` checks that every authored alias key is present and
  that each alias's candidate is an existing contribution whose
  source/descriptor/policy agree with the selected binding for _that
  candidate's_ identity. It never checks that some authored
  `ToolAliasAssignment` actually maps `alias -> candidate.Identity`. A custom
  `IToolCatalogMergePolicy` (the merge policy is a replaceable DI service) can
  therefore return `{ "read": <candidate for tool Y> }` when `read` was authored
  for tool X, and the snapshot will publish `ProviderAliases["read"] = Y`. That
  is alias redirection to a different tool, which the invariants forbid
  ("explicit alias evidence must remain coherent", "cannot ... infer aliases").
  The existing `Apply_WhenAliasDisagreesWithSelectedBinding_*` tests only cover
  source/policy disagreement for the _same_ identity.
- **Evidence:**

```csharp
foreach (var candidate in selection.Aliases.Values)
{
    if (!available.Contains(candidate)
        || !selected.TryGetValue(candidate.Identity, out var chosen)
        || candidate.Source != chosen.Source
        || candidate.Tool != chosen.Tool
        || candidate.Toolset.ExecutionPolicy != chosen.Toolset.ExecutionPolicy)
    {
        throw new InvalidOperationException("Catalog alias selection must agree with its selected source, descriptor, and policy.");
    }
}
```

- **Suggested fix:** Iterate `selection.Aliases` as key/value pairs and
  additionally require
  `candidate.Toolset.Aliases.Any(a => a.Alias == alias && a.Tool == candidate.Identity)`
  (i.e. the alias was authored in that candidate's toolset for that identity);
  add a test that binds an authored alias to another existing identity and
  expects `InvalidOperationException`.
- **Confidence:** High

### T03 — Patch hunks with a `\ No newline at end of file` tail are not end-anchored and can match a line prefix

- **Package:** AgentKit.Tools.Patch
- **File:** `src/AgentKit.Tools.Patch/PatchTextPlanner.cs:117`
- **Severity:** Medium
- **Category:** correctness
- **Description:** `IndexOfLineAnchoredBlock` only anchors the _start_ of the
  old block at a line boundary. When the hunk's last source line carries the
  no-newline marker, `oldBlock` ends without `\n`, so `"foo"` matches the first
  three characters of the line `foobar\n` (or of `foo\n` in a file that does
  have a trailing newline). The planner then splices `newBlock` in and leaves
  `bar\n` (or the `\n`) behind, producing a file the patch author never
  described—e.g. file `foobar\n` with hunk `-foo` + no-newline marker yields
  `bar\n` instead of a "no match" rejection. The ambiguity check has the same
  gap. Existing tests
  (`TryApply_WhenRemovedLineOnlyMatchesMidLine_RejectsAsNoMatch`) cover start
  anchoring only.
- **Evidence:**

```csharp
var candidate = text.IndexOf(block, index, StringComparison.Ordinal);
if (candidate < 0)
{
    return -1;
}

if (candidate == 0 || text[candidate - 1] == '\n')
{
    return candidate;
}
```

- **Suggested fix:** When `block` does not end with `'\n'`, additionally require
  `candidate + block.Length == text.Length` (the block must be the file's final,
  unterminated line); apply the same rule in the ambiguity probe.
- **Confidence:** High

### T04 — `set_status` on a session with no plan is reported as a successful, performed mutation

- **Package:** AgentKit.Tools.Plan
- **File:** `src/AgentKit.Tools.Plan/PlanTool.cs:217`
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** `SessionPlanStateStore.SetStatusAsync` returns
  `PlanStateMissing` when no plan exists (`SessionPlanStateStore.cs:172-175`,
  covered by `SetStatusAsync_WhenNoPlanExists_ReturnsMissing`).
  `PlanTool.Project` maps every `PlanStateMissing` to `Success(..., "Missing")`,
  and `Success` hard-codes `SideEffectCertainty.DefinitelyPerformed`. For the
  `get` action this is correct, but for `set_status` the model receives a
  `Succeeded`/`DefinitelyPerformed` outcome with `{"plan":null}` even though
  nothing was mutated and the requested item does not exist—an incorrect
  authoritative terminal record.
- **Evidence:**

```csharp
private static ToolInvocationResult Project(PlanStateResult result) => result switch
{
    PlanStateFound found => Success(...),
    PlanStateMissing => Success(/*lang=json,strict*/ "{\"plan\":null}", "Missing"),
```

```csharp
private static ToolInvocationResult Success(string json, string status) => new(
    new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, OutcomeStatus(status)),
```

- **Suggested fix:** Pass the action into `Project`; for `set_status`/`replace`
  map `PlanStateMissing` to a failure (`InvocationFailed`,
  `DefinitelyNotPerformed`, "No plan exists; use replace first"), and keep the
  `get` mapping as a success with `Observe`-appropriate certainty.
- **Confidence:** High

### T05 — Read window treats a trailing newline as an extra empty line, misreporting `complete`

- **Package:** AgentKit.Tools.Read
- **File:** `src/AgentKit.Tools.Read/ReadFileTool.cs:191`
- **Severity:** Low
- **Category:** correctness
- **Description:** `ApplyRange` splits on `'\n'`, so a file `l1\nl2\n` becomes
  `["l1","l2",""]`. With `limit: 2` (or `DefaultMaximumLines = 2` and no offset)
  `count = 2`, `complete = 2 >= 3` is `false`, and the caller is told more lines
  remain; a follow-up read at `offset: 3` returns an empty string. Conversely an
  explicit range that reaches the end yields a phantom empty last line. Tests
  only use fixtures without a trailing newline.
- **Evidence:**

```csharp
var lines = content.ReplaceLineEndings("\n").Split('\n');
var startIndex = Math.Min(Math.Max((offset ?? 1) - 1, 0), lines.Length);
var count = Math.Max(Math.Min(limit, lines.Length - startIndex), 0);
var complete = startIndex + count >= lines.Length;
```

- **Suggested fix:** Drop the final empty element when `content` ends with a
  line terminator (tracking that the file had a trailing newline), or compute
  `complete` against the logical line count that excludes it.
- **Confidence:** High

### T06 — Redirect targets bypass the tool's own URL admission checks

- **Package:** AgentKit.Tools.Web
- **File:** `src/AgentKit.Tools.Web/WebFetchTool.cs:168`
- **Severity:** Low
- **Category:** security
- **Description:** `TryDestination` rejects non-`http(s)` schemes, userinfo, and
  fragments for the initial URL, but when the transport returns
  `NetworkRedirectReceived` the tool adopts `redirect.Destination` verbatim.
  `NetworkDestination` accepts any non-blank scheme, so a
  `Location: ftp://…`/`file://…` or scheme-downgrade `https -> http` redirect is
  re-authorized and re-resolved without the tool-level policy that gated the
  first hop. The resolver/authority/transport still see each hop, so this is
  defense-in-depth rather than a direct hole, but the tool's admission rules are
  inconsistent between hop 0 and hop N.
- **Evidence:**

```csharp
if (send is NetworkRedirectReceived redirect)
{
    if (redirectCount >= _options.MaximumRedirects) { ... }
    redirects.Add(SafeDisplayUrl(destination));
    destination = redirect.Destination;
    continue;
}
```

- **Suggested fix:** Apply the same scheme allow-list (and optionally a
  no-downgrade rule) to `redirect.Destination` before continuing, failing with
  `Unsupported`/`PartiallyPerformed` otherwise.
- **Confidence:** Medium

### T07 — Character truncation can split a surrogate pair, emitting a lone surrogate

- **Package:** AgentKit.Tools.Web (also Tools.WebSearch, Tools.Skill,
  Tools.Resource)
- **File:** `src/AgentKit.Tools.Web/WebContentProjector.cs:54`
- **Severity:** Low
- **Category:** correctness
- **Description:** `text[..maximumCharacters]` slices on UTF-16 code units. If
  the cut lands between a high and low surrogate the projected `content` ends
  with an unpaired surrogate; `JsonSerializer` then emits `\uFFFD`/escaped
  garbage and downstream strict UTF-8 consumers (e.g. the tool presenter,
  session codecs) see altered content. The same pattern appears in
  `WebSearchTool.Truncate` (`:373`), `SkillTool` (`:167`), and `ResourceTool`
  (`:188`). The runtime's own `ToolPresenter.TakeValidUnicode` already does this
  correctly by runes.
- **Evidence:**

```csharp
var truncated = text.Length > maximumCharacters;
return new WebContentProjection(
    truncated ? text[..maximumCharacters] : text,
```

- **Suggested fix:** Truncate on rune boundaries (e.g. back off one unit when
  `char.IsHighSurrogate(text[max - 1])`, or enumerate runes as `ToolPresenter`
  does).
- **Confidence:** High

### T08 — `AddAgentTools` and `AddTool<TTool>` omit the `services` null guard required at public boundaries

- **Package:** AgentKit.Tools
- **File:** `src/AgentKit.Tools/ServiceExtensions.cs:280`
- **Severity:** Low
- **Category:** validation
- **Description:** Every other extension in this file begins with
  `ArgumentNullException.ThrowIfNull(services)`; `AddAgentTools` (`:280`) and
  `AddTool<TTool>` (`:383`) do not, so a null collection surfaces as a
  `NullReferenceException` from inside `AddToolPresentation`/`AddSingleton`
  rather than the documented `ArgumentNullException`. This violates the
  repository rule that public members enforce documented argument constraints
  before any effect.
- **Evidence:**

```csharp
public IServiceCollection AddAgentTools(Action<AgentToolsOptions>? configure = null)
{
    _ = services.AddToolPresentation();
    ...
public IServiceCollection AddTool<TTool>()
    where TTool : class, ITool
{
    _ = services.AddSingleton<ITool, TTool>();
```

- **Suggested fix:** Add `ArgumentNullException.ThrowIfNull(services);` as the
  first statement of both members and a `ParamName` test for each.
- **Confidence:** High

### Areas reviewed (Group 04)

- `AgentKit.Tools`: `ToolSchemaProcessor`, `ToolSchemaNumber`,
  `CompiledToolSchema`, `BoundedToolSchemaEngine` (keyword vocabulary,
  `required`, `additionalProperties` default, integer-vs-number, exact decimal
  comparison, rune-counted `minLength`, `enum`/`const` deep equality,
  duplicate-key rejection, depth/node/byte/work charging order);
  `DefaultToolInvoker` (cancellation vs failure, correlation, terminal
  outcomes); `ToolCatalog`, `AllowListToolAuthorizer`; `ToolCatalogMerger`,
  `ToolCatalogMergeGraph`, `RejectingToolCatalogMergePolicy` (deterministic
  ordering, collision set, alias handling); `ToolCatalogDiscovery`,
  `ToolDiscoveryCapture`, `ToolCatalogCapture`, `ToolProviderCapture`,
  `ToolInvokerLease`, `StaticToolProvider` (ownership handoff,
  partial-acquisition release, cleanup-failure aggregation, repeated dispose,
  borrowed invokers); `ToolRegistrationCatalog`, `ToolServiceRegistration`,
  `ServiceExtensions`; `ToolPresenter`; `ToolResultProjectionPolicyCatalog`.
- Feature tools (main tool class + argument parsing + result mapping): Read,
  Write, Edit, Glob, Search, List, Command, Patch (parser + planner + tool),
  Plan (tool + `SessionPlanStateStore`), Question, Task, Skill, Resource, Web
  (`WebFetchTool` + `WebContentProjector`), WebSearch, Language. Presentation
  formatters were checked for exception handling around `JsonDocument.Parse`.
- Cross-cutting greps: `Regex`/`MatchTimeout` (none in scope),
  `UtcNow`/`Guid.NewGuid` (only in injectable default generators),
  `catch (Exception` (all guarded by an `OperationCanceledException` filter or
  observational), `JsonDocument.Parse` sites.
- Mirror tests consulted: `ToolCatalogMergeGraphTests`, `ReadFileToolTests`,
  `PatchToolTests`, `PatchTextPlannerTests`,
  `SessionPlanStateStoreTests`/`PlanToolTests`, `BoundedToolSchemaEngineTests`,
  JSON Schema 2020-12 test-data inventory.

### Areas not reviewed (Group 04)

- Host-side implementations behind the tool contracts (`IFileSystem`,
  `IFileSnapshotReader`, `IAtomicFileReplacer`, `IGlobber`, `IFileSearcher`,
  `IDirectoryReader`, `IProcessRunner`/`IProcessIntentResolver`,
  `INetworkResolver`/`INetworkTransport`, `ILanguageService`,
  `IWorkspacePatchApplier`): path canonicalization, symlink handling, traversal
  bounds, regex timeouts, process kill/timeout, response-size enforcement, and
  second-stage grant enforcement live there, outside this group.
- The loop/conversation executor that turns `ResolvedToolInvocation` into the
  authoritative `ToolCallResult` and `ToolResultPart` (in
  `AgentKit.Conversations`/`AgentKit.Loop`), including
  one-terminal-record/one-projection and retry semantics.
- Observation helper types (`Tool*Observation`, `Tool*Metrics`, `ToolLog`)
  beyond confirming they are exception-isolated.
- `AgentKit.Tools.FileSystem` (empty), `AgentKit.Abstractions` value-object
  validation beyond what was needed to confirm tool behavior, and
  `TodoTool`/`GuidPlanIdGenerator` in Tools.Plan.
- Security-binding fingerprint definitions (`*SecurityBinding` in Abstractions)
  were used, not audited.

## Group 05 — Providers core and OpenAI wire family

Read-only review of `AgentKit.Providers`, `AgentKit.Providers.OpenAICompatible`,
and the nine branded leaves on that wire family (OpenAI, AzureOpenAI,
OpenRouter, Ollama, XAI, DeepSeek, MoonshotKimi, ZAI, Groq). The shared SSE
reader, chat-completions parser, and request translator are solid on the classic
fragmentation/UTF-8/CRLF/`[DONE]`/tool-call-index hazards (and the mirror tests
cover them). The real defects cluster around (a) the wire DTOs assuming OpenAI's
exact error shape, which breaks OpenRouter error handling outright, (b) a
handful of paths where the adapter/parser throws instead of returning the typed
terminal outcome the `ILlmModel` contract promises, (c) a capability-downgrade
decision that is reported but never applied, and (d) endpoint/DI/credential
hygiene. Totals: **1 High, 5 Medium, 6 Low** (12 findings).

---

### P01 — OpenRouter error payloads carry a numeric `error.code`, which fails DTO deserialization and misclassifies every OpenRouter error

- **Package:** AgentKit.Providers.OpenAICompatible (affects
  AgentKit.Providers.OpenRouter)
- **File:**
  `src/AgentKit.Providers.OpenAICompatible/Wire/OpenAIErrorDetail.cs:26` (also
  `OpenAIChatCompletionResponseParser.cs:268`,
  `OpenAICompatibleLlmModelBase.cs:423`)
- **Severity:** High
- **Category:** serialization
- **Description:** OpenRouter's documented error shape is
  `{"error":{"code":429,"message":"...","metadata":{"error_type":"rate_limit_exceeded"}}}`
  — `code` is an HTTP-status **number**, both in HTTP error bodies and in
  mid-stream SSE error chunks (which OpenRouter sends with HTTP 200).
  `OpenAIErrorDetail.Code` is `string?`; System.Text.Json does not coerce a JSON
  number into a `string` and throws `JsonException`. Consequences: (1) every
  OpenRouter mid-stream error chunk hits the `catch (JsonException)` in
  `ParseStreamingAsync` and is reported as `ProtocolViolation` "malformed
  streaming chunk" instead of `Throttling`/`Unavailable`/etc. with the provider
  code retained; (2) every OpenRouter non-2xx body throws inside
  `BuildHttpFailureAsync`, so the failure is status-only and the provider
  message/code are lost; (3) OpenRouter's documented non-streaming "200 with
  `{error}` and no `choices`" body fails on the `required Choices` member and is
  reported as "response body could not be parsed". The OpenRouter test project
  has only `success.json`/`embedding_success.json` fixtures, so none of this is
  exercised. Even if deserialization succeeded, `FailWithProviderErrorAsync`
  keys the kind off `error.type`, which OpenRouter does not send (it uses
  `error.metadata.error_type`), so the kind would still be `Unknown`.
- **Evidence:**

  ```csharp
  // OpenAIErrorDetail.cs
  [JsonPropertyName("code")]
  public string? Code { get; set; }

  // OpenAIChatCompletionResponseParser.cs (streaming)
  chunk = JsonSerializer.Deserialize<OpenAIChatCompletionChunk>(payload, _serializerOptions)
      ?? throw new JsonException("The chunk payload deserialized to a null value.");
  }
  catch (JsonException exception)
  {
      return await FailAsync(observer, context, sequence,
          "The provider returned a malformed streaming chunk.", exception, ...
  ```

- **Suggested fix:** Make `Code` a `JsonElement?`/custom-converter field that
  accepts string or number (map a numeric code through
  `HttpStatusFailureKindMapper`), read `error.metadata.error_type` as a fallback
  for kind mapping, make `Choices` optional on `OpenAIChatCompletionResponse`
  and route a top-level `error` on a 200 body through the same provider-error
  path; add OpenRouter error fixtures (HTTP 402/429 body, mid-stream error
  chunk, 200-with-error body).
- **Confidence:** High
- **Status:** Fixed ✅

---

### P02 — Buffered parser throws `ArgumentException` (no terminal event) on empty/whitespace tool-call `id`/`name` or whitespace `model`/`id`

- **Package:** AgentKit.Providers.OpenAICompatible
- **File:**
  `src/AgentKit.Providers.OpenAICompatible/OpenAIChatCompletionResponseParser.cs:196`
  (and `:198`, `:215`, `:526`)
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** `ILlmModel.ExecuteAsync` promises "the same terminal outcome
  as the last event delivered". In `ParseBufferedAsync` a tool call with
  `"id": ""` or `"name": ""`/`"   "` (both pass `required string`
  deserialization) reaches `new ProviderToolCallId(toolCall.Id)` /
  `new ToolAlias(toolCall.Function.Name)`, whose `ThrowIfNullOrWhiteSpace`
  guards throw `ArgumentException`. The exception escapes the parser and
  `ExecuteAsync` (which only catches `OperationCanceledException`/`IOException`
  around parsing) after `ModelResponseStarted` and possibly part events were
  already delivered, so the observer never receives a terminal event and the
  caller gets an exception instead of a `ModelAttemptFailed(ProtocolViolation)`.
  The streaming path guards both (`Length > 0` normalisation, nameless-slot
  fail-closed) but the buffered path does not. The same guard gap exists for
  whitespace-only `model`/`id` strings in `CreateResponseIdentity` (`Length > 0`
  check, but `ModelId`/`ProviderResponseId` reject whitespace).
- **Evidence:**

  ```csharp
  var toolCallPart = new ToolCallPart(
      callId,
      new ToolReference(new ToolAlias(toolCall.Function.Name), null, null),
      arguments,
      new ProviderToolCallId(toolCall.Id),
      ExtensionData.Empty);
  ```

- **Suggested fix:** Normalise/validate `Id`, `Function.Name`, `Model`, and `Id`
  in the buffered path exactly like the streaming path (treat empty/whitespace
  as absent; fail closed with a typed `ProtocolViolation` when a tool name is
  missing), and consider a last-resort `catch (ArgumentException)` in the parser
  that converts to `FailAsync`.
- **Confidence:** High
- **Status:** Fixed ✅

---

### P03 — `DefaultModelSelector` accepts a `CapabilitiesDowngraded` candidate but the declared adjustment is dropped, so a parallel-tool-call downgrade later fails preflight

- **Package:** AgentKit.Providers
- **File:** `src/AgentKit.Providers/DefaultModelSelector.cs:86-96` (with
  `DefaultModelCapabilityValidator.cs:100-112` and
  `ModelRequestPreflight.cs:80`)
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** Under `CapabilityDowngradePolicy.AllowDeclaredAdjustments`
  the validator returns `CapabilitiesDowngraded` with a `ParallelToolCalls`
  adjustment ("Parallel tool calls were disabled"). The selector turns that into
  `ModelSelected` but `ModelSelectionDecision` has no adjustments member — only
  the diagnostic string survives. Nothing downstream applies the adjustment:
  `DefaultAgentLoop` forwards `request.Settings` unchanged, and
  `ModelRequestPreflight.Validate` then rejects
  `Settings.ParallelToolCalls == true` against a model lacking
  `SupportsParallelToolCalls` with `InvalidRequest` — at run time, in the middle
  of the run, after selection said the model was acceptable. When
  `ParallelToolCalls` is null the adjustment is a silent no-op (no
  `parallel_tool_calls:false` is sent). Only the Streaming adjustment happens to
  work because the adapter independently checks `SupportsStreaming`.
- **Evidence:**

  ```csharp
  diagnostics.Add(new ModelSelectionDiagnostic(
      alias,
      ModelCandidateOutcome.Selected,
      validation is CapabilitiesDowngraded downgraded
          ? $"Selected with {downgraded.Adjustments.Length} declared capability adjustment(s)."
          : "Selected with full capability support."));
  ...
  return new ModelSelected(new ModelSelectionDecision(descriptor, catalog.Version, ..., diagnostics.ToImmutable()));
  ```

- **Suggested fix:** Carry `ImmutableArray<CapabilityAdjustment>` on
  `ModelSelectionDecision` and have the loop apply them to `LlmRequestSettings`
  (e.g., `ParallelToolCalls = false`) before building the request, or refuse to
  downgrade `ParallelToolCalls` when the run's settings explicitly request
  `true`.
- **Confidence:** Medium
- **Status:** Fixed ✅

---

### P04 — Base address without trailing slash silently drops its last path segment when the operation path is combined

- **Package:** AgentKit.Providers.OpenAICompatible (all leaves)
- **File:**
  `src/AgentKit.Providers.OpenAICompatible/OpenAICompatibilityProfile.cs:172`
  (validation at `OpenAICompatibleEndpointOptionsValidation.cs:61`)
- **Severity:** Medium
- **Category:** validation
- **Description:** `ChatCompletionsUri => new(BaseAddress, ChatCompletionsPath)`
  uses RFC 3986 relative resolution. Every leaf's default base ends in `/` and
  its path is relative (`chat/completions`), so defaults work — but
  user-configured bases like `http://ollama-host:11434/v1`,
  `https://my-proxy/openai/v1`, or `https://api.groq.com/openai/v1` (all
  extremely common when binding from configuration) resolve to
  `http://ollama-host:11434/chat/completions`, i.e. `/v1` is silently discarded
  and the request 404s with an opaque `InvalidRequest`. The options validator
  only checks "absolute URI" and "non-rooted relative path"; nothing requires or
  normalises a trailing slash and the option docs do not mention it.
- **Evidence:**

  ```csharp
  /// <summary>Gets the absolute URI of the chat completions operation.</summary>
  public Uri ChatCompletionsUri => new(BaseAddress, ChatCompletionsPath);
  ...
  public Uri? EmbeddingsUri => EmbeddingsPath is null ? null : new Uri(BaseAddress, EmbeddingsPath);
  ```

- **Suggested fix:** Normalise `BaseAddress` in the profile constructor (append
  `/` when `AbsolutePath` does not end with one) or reject a
  non-slash-terminated path in `OpenAICompatibleEndpointOptionsValidation` with
  an explicit message.
- **Confidence:** High
- **Status:** Fixed ✅

---

### P05 — Credential-source failures escape `ExecuteAsync`/`GenerateAsync` as raw exceptions instead of a typed `Authentication` failure

- **Package:** AgentKit.Providers.OpenAICompatible
- **File:**
  `src/AgentKit.Providers.OpenAICompatible/OpenAICompatibleLlmModelBase.cs:243`
  (same in `OpenAICompatibleEmbeddingModelBase.cs:210`)
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** `IProviderCredentialSource.GetCredentialAsync` is
  user-supplied (e.g. `DelegatingOAuthCredentialSource` wrapping an
  Entra/Azure.Identity token provider for `AddAzureOpenAIOAuthCredential`).
  Token acquisition routinely fails with provider-specific exceptions
  (`AuthenticationFailedException`, `HttpRequestException`, `TimeoutException`,
  or an `OperationCanceledException` from the token provider's own internal
  timeout while the caller token is not cancelled). Only
  `OperationCanceledException when cancellationToken.IsCancellationRequested` is
  caught; anything else propagates out of `ExecuteAsync` with no
  `ModelResponseFailed` delivered, breaking the "terminal outcome equals last
  event" contract and bypassing the failure taxonomy (`Authentication`) that
  every other auth failure in this class uses.
- **Evidence:**

  ```csharp
  try
  {
      credential = await _credentials.GetCredentialAsync(Descriptor.ProviderId, cancellationToken).ConfigureAwait(false);
  }
  catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
  {
      return await CancelAsync().ConfigureAwait(false);
  }
  ```

- **Suggested fix:** Add
  `catch (Exception exception) when (exception is not OperationCanceledException)`
  that calls `FailWithKindAsync(ProviderFailureKind.Authentication, ...)` with a
  "credential could not be resolved" message and the exception, and map a
  non-caller `OperationCanceledException` to `Timeout` like the transport path
  does.
- **Confidence:** Medium
- **Status:** Fixed ✅

---

### P06 — `ApiKeyProviderCredential` / `OAuthTokenProviderCredential` are plain records: default `ToString()` prints the secret

- **Package:** AgentKit.Abstractions (contract consumed by every leaf's
  `Add<Provider>ApiKeyCredential` / `StaticApiKeyCredentialSource`)
- **File:** `src/AgentKit.Abstractions/Providers/ApiKeyProviderCredential.cs:33`
  (also `OAuthTokenProviderCredential.cs`)
- **Severity:** Medium
- **Category:** security
- **Description:** Both credential records rely on the compiler-generated record
  `ToString()`/`PrintMembers`, which renders
  `ApiKeyProviderCredential { ApiKey = sk-live-... }`.
  `ProviderAuthorizationGranted` in `AgentKit.Providers` was deliberately given
  a redacting `PrintMembers`, but the credential that flows through
  `IProviderCredentialSource`, hook arguments, structured-logging destructuring
  (`{@credential}`), debugger displays, and exception messages is the unredacted
  one. This contradicts the repo rule that credentials never enter
  logs/diagnostics.
- **Evidence:**

  ```csharp
  public sealed record ApiKeyProviderCredential: ProviderCredential
  {
      public ApiKeyProviderCredential(string apiKey)
      {
          ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
          ApiKey = apiKey;
      }
      public string ApiKey { get; init; }
  }
  ```

- **Suggested fix:** Override `ToString()`/`PrintMembers` on both records (and
  the abstract base) to emit `[REDACTED]`, mirroring
  `ProviderAuthorizationGranted`; add a test asserting the secret is absent from
  `ToString()`.
- **Confidence:** High
- **Status:** Fixed ✅

---

### P07 — Embedding parser throws `InvalidOperationException` (uncaught) when a vector element is not a JSON number

- **Package:** AgentKit.Providers.OpenAICompatible
- **File:**
  `src/AgentKit.Providers.OpenAICompatible/OpenAIEmbeddingResponseParser.cs:191`
- **Severity:** Low
- **Category:** contract-mismatch
- **Description:** `DecodeVector` calls `element.GetSingle()` on each array
  element. `JsonElement.GetSingle()` throws `InvalidOperationException` (not
  `JsonException`/`FormatException`) when the element's `ValueKind` is not
  `Number` (e.g. `null`, a string, a nested array from a misbehaving compatible
  server). The surrounding
  `catch (Exception) when (exception is JsonException or FormatException)` does
  not cover it, so the exception escapes `ParseAsync` and `GenerateAsync`
  instead of producing the typed `ProtocolViolation` used for every other
  malformed-vector case.
- **Evidence:**

  ```csharp
  foreach (var element in embedding.EnumerateArray())
  {
      values.Add(element.GetSingle());
  }
  ...
  catch (Exception exception) when (exception is JsonException or FormatException)
  ```

- **Suggested fix:** Check `element.ValueKind == JsonValueKind.Number` / use
  `TryGetSingle` and throw `JsonException` on mismatch, or widen the filter to
  include `InvalidOperationException`.
- **Confidence:** High
- **Status:** Fixed ✅

---

### P08 — A request deadline more than ~49.7 days away makes the deadline `CancellationTokenSource` constructor throw

- **Package:** AgentKit.Providers.OpenAICompatible
- **File:**
  `src/AgentKit.Providers.OpenAICompatible/OpenAICompatibleLlmModelBase.cs:280`
  (same at `OpenAICompatibleEmbeddingModelBase.cs:245`)
- **Severity:** Low
- **Category:** validation
- **Description:** `remaining = request.Deadline - now` is only checked for
  `<= TimeSpan.Zero`. `new CancellationTokenSource(TimeSpan, TimeProvider)`
  throws `ArgumentOutOfRangeException` when the delay exceeds
  `uint.MaxValue - 1` ms (~49.7 days). `AgentRunRequest.AttemptTimeout` is
  validated only as `> TimeSpan.Zero` and `LlmModelRequest.Deadline` is an
  unconstrained `DateTimeOffset`, so a caller expressing "no practical deadline"
  (e.g. `TimeSpan.FromDays(365)` or a far-future `DateTimeOffset`) turns every
  attempt into an unhandled exception after credential resolution and
  translation have already run.
- **Evidence:**

  ```csharp
  var remaining = request.Deadline - _timeProvider.GetUtcNow();
  if (remaining <= TimeSpan.Zero) { ... Timeout ... }
  ...
  using var deadlineSource = new CancellationTokenSource(remaining, _timeProvider);
  ```

- **Suggested fix:** Clamp `remaining` to the CTS maximum (or use `CancelAfter`
  guarded by the bound) and/or add an upper-bound validation on `AttemptTimeout`
  at the composition boundary.
- **Confidence:** High
- **Status:** Fixed ✅

---

### P09 — LLM preflight ignores `SupportsReasoning`/`SupportsSystemInstructions`, so `reasoning_effort` and `system`/`developer` messages are sent to models that declare no support

- **Package:** AgentKit.Providers
- **File:** `src/AgentKit.Providers/ModelRequestPreflight.cs:72-83`
- **Severity:** Low
- **Category:** validation
- **Description:** `ModelRequestPreflight.Validate` fails closed only for tool
  calls and parallel tool calls. `Settings.ReasoningEffort` is translated to
  `reasoning_effort` unconditionally even when
  `Descriptor.Capabilities.SupportsReasoning` is false (the default for every
  leaf's `DefaultCapabilities`), and `SystemMessage`/`DeveloperMessage` are
  emitted regardless of `SupportsSystemInstructions`. The request then fails at
  the provider with a 400 `InvalidRequest` mid-run rather than at the boundary,
  and for permissive compatible servers the parameter is silently ignored — the
  opposite of the explicit-capability rule the tool-call checks implement.
- **Evidence:**

  ```csharp
  return request.Context switch
  {
      { Model: var model } when model != descriptor => Reject(...),
      { Tools.Length: 0 } => null,
      _ when !capabilities.SupportsToolCalls => Reject(...),
      { Settings.ParallelToolCalls: true } when !capabilities.SupportsParallelToolCalls => Reject(...),
      _ => null,
  };
  ```

- **Suggested fix:** Add
  `Settings.ReasoningEffort is not null && !SupportsReasoning` and
  `messages contain System/Developer && !SupportsSystemInstructions` rejections
  (or explicit downgrades) to preflight.
- **Confidence:** Medium
- **Status:** Fixed ✅

---

### P10 — Embedding translator ignores `EmbeddingCapabilities` (`SupportsDimensions`, `SupportsEncodingSelection`)

- **Package:** AgentKit.Providers.OpenAICompatible
- **File:**
  `src/AgentKit.Providers.OpenAICompatible/OpenAIEmbeddingRequestTranslator.cs:59`
  (and `ModelRequestPreflight.cs` embedding overload)
- **Severity:** Low
- **Category:** validation
- **Description:** The translator emits `dimensions` and `encoding_format`
  whenever the request sets them; neither the translator nor the embedding
  `ModelRequestPreflight` overload consults the descriptor's
  `EmbeddingCapabilities`. A descriptor registered with
  `supportsDimensions: false` (e.g. `text-embedding-ada-002`, most Ollama
  embedding models) still sends `dimensions`, which OpenAI rejects with 400 and
  some compatible servers silently ignore — producing vectors of a different
  dimensionality than the caller's `EmbeddingSpaceIdentity` expects, which is
  exactly the vector-space mismatch the storage contract is meant to prevent.
- **Evidence:**

  ```csharp
  if (embeddingRequest.Dimensions is { } dimensions)
  {
      body["dimensions"] = dimensions;
  }
  if (embeddingRequest.Encoding is { } encoding)
  {
      body["encoding_format"] = TranslateEncoding(encoding);
  }
  ```

- **Suggested fix:** Extend the embedding preflight to reject
  `Dimensions`/`Encoding` when the descriptor's capabilities do not advertise
  them, and have the parser assert the returned dimensionality equals the
  requested `dimensions` when one was sent.
- **Confidence:** Medium
- **Status:** Fixed ✅

---

### P11 — Error bodies whose `error` member is a string (xAI shape) throw during error-body parsing and lose the provider code/message

- **Package:** AgentKit.Providers.OpenAICompatible (affects
  AgentKit.Providers.XAI)
- **File:**
  `src/AgentKit.Providers.OpenAICompatible/OpenAICompatibleLlmModelBase.cs:422-426`
  (same in the embedding base)
- **Severity:** Low
- **Category:** serialization
- **Description:** xAI's chat/embeddings endpoints return errors as
  `{"code":"<status text>","error":"<message>"}` — `error` is a string and
  `code` is top-level. Deserialising that into `OpenAIErrorResponse` (where
  `Error` is an object) throws `JsonException`, which the generic catch turns
  into a status-only failure with the parse exception as `DiagnosticCause`; the
  provider's `code`/message never reach
  `ProviderCode`/`ProviderErrorMessageEvidence`. The HTTP-status kind mapping
  still works, but the leaf-specific evidence the taxonomy is supposed to retain
  is dropped for every xAI error.
- **Evidence:**

  ```csharp
  var error = await JsonSerializer
      .DeserializeAsync<OpenAIErrorResponse>(body, cancellationToken: cancellationToken)
      .ConfigureAwait(false);
  providerMessage = error?.Error?.Message;
  providerCode = error?.Error?.Code ?? error?.Error?.Type;
  ```

- **Suggested fix:** Parse the error body as `JsonDocument` and accept both
  shapes (`error` object vs. `error` string + top-level `code`), or let the
  branded leaf supply an error-body reader; add an xAI error fixture.
- **Confidence:** Medium
- **Status:** Fixed ✅

---

### P12 — Every leaf registers a raw `new HttpClient()` singleton (infinite pooled-connection lifetime, no factory/resilience, first-registration wins)

- **Package:** AgentKit.Providers.OpenAI (identical in AzureOpenAI, OpenRouter,
  Ollama, XAI, DeepSeek, MoonshotKimi, ZAI, Groq)
- **File:** `src/AgentKit.Providers.OpenAI/ServiceExtensions.cs:65`
- **Severity:** Low
- **Category:** resource-leak
- **Description:** `TryAddSingleton(_ => new HttpClient())` creates a
  process-lifetime `HttpClient` over a default `SocketsHttpHandler` whose
  `PooledConnectionLifetime` is infinite, so DNS changes for `api.openai.com`,
  Azure resource endpoints, or an internal proxy are never observed until the
  process restarts (the well-documented singleton-HttpClient pitfall). Because
  it is an unkeyed `TryAdd` on a shared framework type, whichever
  `Add<Provider>()` runs first wins for all providers, and a later
  `services.AddHttpClient()`/resilience configuration by the application is
  silently ignored (that registration is also `TryAdd`). All leaves then resolve
  this one client via `GetRequiredService<HttpClient>()`.
- **Evidence:**

  ```csharp
  services.TryAddSingleton(TimeProvider.System);
  services.TryAddSingleton(_ => new HttpClient());
  return services;
  ```

- **Suggested fix:** Register the client through `IHttpClientFactory` under a
  provider-keyed name (e.g. `AddHttpClient("agentkit.openai")`) and inject it
  via `IHttpClientFactory`/keyed service, or at minimum build the fallback
  client over a
  `SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }`.
- **Confidence:** Medium
- **Status:** Fixed ✅

---

### Areas reviewed (Group 05)

- `AgentKit.Providers/Http/ServerSentEventReader.cs` — line termination
  (LF/CR/CRLF incl. split pairs), BOM, comments, multi-line `data:`,
  `id:`/`retry:`/unknown fields, end-of-stream dispatch, cancellation
  propagation, stream ownership.
- `AgentKit.Providers.OpenAICompatible/OpenAIChatCompletionResponseParser.cs` —
  buffered and streaming state machines, `[DONE]`, usage-before/after-finish
  ordering, `choices:[]` usage chunks, multi-choice rejection, tool-call slot
  resolution (index-keyed, id-keyed, sparse indexes, id only on first delta,
  conflicting ids), partial-part retention on failure, in-stream `error` frames,
  stop-reason mapping, usage mapping (`NotReported` vs `Final`, cached/reasoning
  tokens).
- `OpenAIRequestTranslator.cs` — role mapping (no runtime→system promotion;
  `RuntimeMessage` → user envelope), developer/system profile switch, assistant
  tool_calls / `reasoning_content` replay, tool-result `tool_call_id`
  correlation via `ProviderToolCallIds`, `n` pinning and extension precedence,
  `max_tokens` vs `max_completion_tokens`, `reasoning_effort`, tool/tool_choice
  translation, media parts (fail-closed).
- `OpenAICompatibleLlmModelBase.cs` / `OpenAICompatibleEmbeddingModelBase.cs` —
  preflight, deadline handling, credential→header (`Bearer` vs Azure `api-key`),
  `ResponseHeadersRead`, `HttpClient.Timeout` interaction with streamed bodies
  (verified the timeout CTS is disposed after headers), request/response
  disposal, caller-cancel vs deadline vs transport-timeout classification,
  error-body reading, `Retry-After` (delta and HTTP-date), request-id header,
  `SequencingModelResponseObserver` terminal guarantees.
- `OpenAICompatibilityProfile.cs`,
  `OpenAICompatibleEndpointOptionsValidation.cs`, `ServiceExtensions.cs`
  (compatible + all nine leaves), each leaf's `*ProviderDefaults.cs`,
  `*ProviderOptions.cs`, `*LlmModel.cs`, Azure deployment/`api-key` handling,
  OpenRouter attribution headers (verified against current OpenRouter docs),
  endpoint defaults.
- `AgentKit.Providers` — `DefaultModelCatalog`, `DefaultModelSelector`,
  `DefaultModelCapabilityValidator`, `DefaultLlmModelResolver`,
  `ModelRequestPreflight`, `ProviderJson`, `ProviderToolCallIds`,
  `RuntimeMessageProjection`, `ProviderResponseParseContext`, `KnownModel*`,
  `HttpStatusFailureKindMapper`, `RetryAfterResolver`,
  `ProviderRequestIdReader`, `ProviderErrorMessageEvidence`,
  `ProviderAuthorization*`.
- Wire DTOs, `OpenAIEmbeddingRequestTranslator`/`OpenAIEmbeddingResponseParser`
  (float and base64 decoding, index reconciliation).
- Relevant contracts in `AgentKit.Abstractions` (`LlmModelRequest`,
  `LlmRequestSettings`, `ModelCapabilities`, `ModelUsage`, `ProviderFailure`,
  credential records, `SequencingModelResponseObserver`,
  `DelegatingOAuthCredentialSource`) and the mirror test lists for the parser,
  LLM base, and OpenRouter leaf.

### Areas not reviewed (Group 05)

- Non-OpenAI-family provider packages (Anthropic, AwsBedrock, Cohere,
  GoogleGemini, GoogleVertexAI, MistralAI) beyond confirming they share the same
  `HttpClient` registration pattern.
- Full contents of `tests/AgentKit.Providers.Tests/Http/*` and
  `ServerSentEventReader` unit tests (only test names/fixtures were consulted
  where needed to confirm intent).
- `KnownModelCatalog` embedded resource contents (`known-models.json`) for
  model-ID/pricing accuracy.
- `DefaultEmbeddingModelResolver`, `StaticModelDescriptorSource`,
  `ProviderLog`/`ProviderMetrics` bodies (skimmed only for content-logging
  risk).
- Observability instrumentation completeness (activity/metric coverage) in the
  adapters.
- Live provider behaviour beyond the OpenRouter docs fetched; the xAI error-body
  shape (P11) is from prior knowledge, not a fetched spec.

## Group 06 — Anthropic, Cohere, MistralAI, AwsBedrock, GoogleGemini, GoogleVertexAI

Read-only review of the six branded provider packages with their own wire
protocols, plus the shared `ServerSentEventReader`, `ProviderToolCallIds`,
`ProviderJson`, and HTTP helpers they depend on. The SSE reader
(StreamReader-based, CRLF/split-CR/UTF-8 safe), the hand-rolled AWS event-stream
decoder (prelude CRC, message CRC, bounded lengths, header parsing), the SigV4
signer (double-encoded canonical path, sorted query, session token,
`TimeProvider`-sourced timestamp), credential redaction, keyed credential
registration, `ResponseHeadersRead` + token-passing body reads, and
cancellation/deadline classification are all sound. The defects found cluster
around response multiplicity (Mistral and Gemini silently take
`choices[0]`/`candidates[0]` and merge streamed choices), embedding vector-space
identity (Gemini/Vertex drop the requested purpose), Bedrock request shaping
(ARN path escaping, non-alternating roles), and a few robustness gaps where
provider input can escape as an unhandled exception or lose error evidence.
Totals: **0 High, 6 Medium, 6 Low**.

### N01 — Mistral parser silently keeps only `choices[0]` and merges streamed choices without checking `index`

- **Package:** AgentKit.Providers.MistralAI
- **File:** `src/AgentKit.Providers.MistralAI/MistralAIResponseParser.cs:86`
  (buffered) and `:237` (streaming)
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** The translator never pins `n` to 1 (unlike
  `OpenAIRequestTranslator`, which sets `["n"] = 1`) and the parser neither
  rejects nor reports extra choices. Buffered responses with `n > 1` (reachable
  through `Settings.Extensions`/`Options.Extensions`) discard every choice after
  the first; in streaming, `chunk.Choices[0]` is taken from each chunk
  regardless of `choice.index`, so deltas for choice 1 are appended to the same
  text slot and tool-call slots as choice 0, producing corrupted text and tool
  arguments. The OpenAI-compatible family fails closed on `Choices.Count > 1`;
  this adapter violates the "single-candidate operation rejects extra choices"
  invariant.
- **Evidence:**

  ```csharp
  var choice = dto.Choices?.Count > 0 ? dto.Choices[0] : null;   // buffered, line 86
  ...
  var choice = chunk.Choices?.Count > 0 ? chunk.Choices[0] : null; // streaming, line 237
  if (chunk.Usage is not null) { usage = chunk.Usage; ... }
  if (choice is null) { continue; }
  finishReason = choice.FinishReason ?? finishReason;
  ```

- **Suggested fix:** Pin `n = 1` in `MistralAIRequestTranslator` and fail closed
  in the parser when `Choices.Count > 1` (buffered) or when a streamed
  `choice.index != 0` is observed, mirroring
  `OpenAIChatCompletionResponseParser`.
- **Confidence:** High
- **Status:** Fixed ✅

### N02 — Gemini parser silently keeps only `candidates[0]` and merges streamed candidates

- **Package:** AgentKit.Providers.GoogleGemini
- **File:**
  `src/AgentKit.Providers.GoogleGemini/GoogleGeminiResponseParser.cs:87`
  (buffered) and `:219` (streaming)
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** `generationConfig.candidateCount` is never pinned to 1 and
  can be supplied through extensions (the translator only emits
  `generationConfig` when a portable setting is present, so an extension
  `generationConfig` is applied verbatim). With `candidateCount > 1`, buffered
  parsing drops all but the first candidate, and streaming takes `Candidates[0]`
  of each chunk without checking `candidate.index`, routing fragments of a
  second candidate into the first candidate's accumulators. This also affects
  `AgentKit.Providers.GoogleVertexAI`, which reuses this parser.
- **Evidence:**

  ```csharp
  var candidate = dto.Candidates?.Count > 0 ? dto.Candidates[0] : null;      // line 87
  ...
  var candidate = chunk.Candidates?.Count > 0 ? chunk.Candidates[0] : null;  // line 219
  if (chunk.UsageMetadata is not null) { usage = chunk.UsageMetadata; usageIsFinal = candidate?.FinishReason is not null; }
  if (candidate is null) { continue; }
  ```

- **Suggested fix:** Emit `generationConfig.candidateCount = 1` in
  `GoogleGeminiContentTranslator` and return a `ProtocolViolation` failure when
  more than one candidate (or a candidate with `index != 0`) is observed.
- **Confidence:** High
- **Status:** Fixed ✅

### N03 — Gemini embedding space identity discards the requested `taskType`

- **Package:** AgentKit.Providers.GoogleGemini
- **File:**
  `src/AgentKit.Providers.GoogleGemini/GoogleGeminiEmbeddingResponseParser.cs:79`
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** `GoogleGeminiEmbeddingRequestTranslator` sends `taskType`
  derived from `EmbeddingRequest.Purpose` (RETRIEVAL_QUERY, RETRIEVAL_DOCUMENT,
  CLASSIFICATION, ...), and Gemini uses asymmetric transforms per task type. The
  parser nevertheless stamps every vector with `EmbeddingPurpose.Unspecified`,
  so vectors computed for different task types carry identical
  `EmbeddingSpaceIdentity` values and a vector store cannot reject cross-space
  comparisons as the `EmbeddingSpaceIdentity` contract requires.
  `CohereEmbeddingResponseParser` correctly uses the requested purpose; this
  parser does not, and the shared `EmbeddingResponseParseContext` does not carry
  it.
- **Evidence:**

  ```csharp
  var space = new EmbeddingSpaceIdentity(
      identity,
      vector.Values.Length,
      EmbeddingElementType.Float32,
      EmbeddingPurpose.Unspecified,
      ExtensionData.Empty);
  ```

- **Suggested fix:** Pass the request's `Purpose` (via a Gemini-specific parse
  context or an added field on `EmbeddingResponseParseContext`) and record it in
  the `EmbeddingSpaceIdentity`.
- **Confidence:** High
- **Status:** Fixed ✅

### N04 — Vertex AI embedding space identity discards the requested `task_type`

- **Package:** AgentKit.Providers.GoogleVertexAI
- **File:**
  `src/AgentKit.Providers.GoogleVertexAI/GoogleVertexAIEmbeddingResponseParser.cs:71`
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** Same defect as N03 for the Vertex `predict` embedding path:
  `GoogleVertexAIEmbeddingRequestTranslator.TranslateInstance` emits a
  per-instance `task_type`, but the parser hardcodes
  `EmbeddingPurpose.Unspecified` into every `EmbeddingSpaceIdentity`,
  misrepresenting query-tuned and document-tuned vectors as the same space.
- **Evidence:**

  ```csharp
  var space = new EmbeddingSpaceIdentity(
      identity,
      vector.Values.Length,
      EmbeddingElementType.Float32,
      EmbeddingPurpose.Unspecified,
      ExtensionData.Empty);
  ```

- **Suggested fix:** Thread the requested purpose into the parse context and use
  it when building the space identity.
- **Confidence:** High
- **Status:** Fixed ✅

### N05 — Bedrock model-ID path escaping leaves `/` unescaped, breaking ARN model IDs

- **Package:** AgentKit.Providers.AwsBedrock
- **File:**
  `src/AgentKit.Providers.AwsBedrock/AwsBedrockProviderDefaults.cs:132`
- **Severity:** Medium
- **Category:** correctness
- **Description:** `EscapeModelId` only replaces `:`; the Converse `modelId` URI
  label is a single non-greedy path segment and ARNs for inference profiles,
  application inference profiles, provisioned/custom models, and prompts all
  contain `/` (e.g. `...:inference-profile/us.anthropic.claude-...`). Emitting
  the raw slash splits the label into extra path segments
  (`/model/arn%3A...%3Ainference-profile/us.anthropic.../converse`), which no
  longer matches `/model/{modelId}/converse` and is routed/validated as a
  different resource. The signer test at
  `tests/.../Signing/AwsSigV4SignerTests.cs:93` even documents that the wire
  form "carries %3A and %2F", but the URI builder never produces `%2F`, and
  `AwsBedrockProviderDefaultsTests.cs:37` locks in the raw-slash form.
- **Evidence:**

  ```csharp
  private static string EscapeModelId(string modelId) => modelId.Replace(":", "%3A", StringComparison.Ordinal);
  ...
  return new Uri(BuildBaseAddress(options.Region), $"/model/{EscapeModelId(modelId)}/converse");
  ```

- **Suggested fix:** Percent-encode the whole label with `Uri.EscapeDataString`
  (which encodes both `:` and `/`) and update the defaults test to expect `%2F`;
  the signer already double-encodes correctly from `Uri.AbsolutePath`.
- **Confidence:** Medium
- **Status:** Fixed ✅

### N06 — Bedrock translator emits consecutive same-role messages that Converse rejects

- **Package:** AgentKit.Providers.AwsBedrock
- **File:**
  `src/AgentKit.Providers.AwsBedrock/AwsBedrockRequestTranslator.cs:146`
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** `UserMessage`, `RuntimeMessage`, and `ToolMessage` are each
  emitted as separate `user` messages with no merging. A `ToolMessage` followed
  by a steering `UserMessage` or a loop-injected `RuntimeMessage` (both common
  in this framework's turn model) yields two consecutive `user` entries;
  likewise two adjacent `AssistantMessage`s yield consecutive `assistant`
  entries. Bedrock Converse validates strict user/assistant alternation for the
  Anthropic Claude family (a `ValidationException` stating that a conversation
  must alternate between user and assistant roles), so these histories fail
  every subsequent turn. Anthropic's direct API merges adjacent same-role turns
  server-side, which is why the sibling `AnthropicMessageTranslator` gets away
  with the same shape; Bedrock does not.
- **Evidence:**

  ```csharp
  case UserMessage:
      result.Add(CreateMessage("user", TranslateUserContent(message.Parts)));
      break;
  case RuntimeMessage:
      result.Add(CreateMessage("user", TranslateRuntimeContent(message.Parts)));
      break;
  ...
  case ToolMessage:
      result.Add(CreateMessage("user", TranslateToolResultContent(message.Parts, providerCallIds)));
      break;
  ```

- **Suggested fix:** Coalesce adjacent messages of the same wire role into one
  message by concatenating their `content` arrays (keeping `toolResult` blocks
  first for the merged user turn), preserving order and losing nothing.
- **Confidence:** Medium
- **Status:** Fixed ✅

### N07 — Bedrock event-stream `:message-type: error` frames are misreported as malformed JSON

- **Package:** AgentKit.Providers.AwsBedrock
- **File:** `src/AgentKit.Providers.AwsBedrock/AwsBedrockResponseParser.cs:190`
- **Severity:** Low
- **Category:** correctness
- **Description:** The AWS event-stream protocol has two failure message types:
  `exception` (JSON payload, `:exception-type` header) and `error`
  (`:error-code` / `:error-message` headers, typically no JSON payload). The
  parser unconditionally JSON-deserializes the payload before inspecting
  `:message-type`, so an `error` frame with an empty or non-JSON payload throws
  `JsonException` and is reported as "The provider returned a malformed
  streaming event" (`ProtocolViolation`), discarding the error code and message
  and misclassifying a service-side failure as a client-visible protocol defect.
- **Evidence:**

  ```csharp
  payload = JsonSerializer.Deserialize<AwsBedrockStreamEventDto>(message.Payload, _serializerOptions)
      ?? throw new JsonException("The event payload deserialized to a null value.");
  ...
  if (message.MessageType == "exception") { ... }
  ```

- **Suggested fix:** Check `message.MessageType` before deserializing; for
  `error`, build the failure from the `:error-code` and `:error-message` headers
  and map the code through `AwsBedrockErrorMapping`.
- **Confidence:** High
- **Status:** Fixed ✅

### N08 — Anthropic streaming: `input_json_delta` on a non-`tool_use` block throws instead of failing typed

- **Package:** AgentKit.Providers.Anthropic
- **File:**
  `src/AgentKit.Providers.Anthropic/AnthropicMessageStreamParser.cs:492`
- **Severity:** Low
- **Category:** correctness
- **Description:** `HandleContentBlockDeltaAsync` routes on `delta.Type` only;
  if the provider (or a proxy) sends an `input_json_delta` for a block whose
  `content_block_start` was `text`/`thinking`/unknown, `accumulator.ToolCallId`
  is null and `!.Value` raises `InvalidOperationException`.
  `AnthropicLlmModel.ExecuteAsync` catches only `OperationCanceledException` and
  `IOException` around parsing, so the exception escapes the adapter and the
  observer never receives a terminal event, violating the fail-closed
  typed-result contract for untrusted provider input.
- **Evidence:**

  ```csharp
  case "input_json_delta":
      if (delta.PartialJson is { Length: > 0 } jsonFragment)
      {
          _ = accumulator.Json.Append(jsonFragment);
          await observer.OnEventAsync(
              new ModelPartDelta(requestId, nextSequence(), index,
                  new ToolArgumentsContentDelta(accumulator.ToolCallId!.Value, jsonFragment)), ...
  ```

- **Suggested fix:** Guard on `accumulator.Type == "tool_use"` (or
  `ToolCallId is { } id`) and return a `ProtocolViolation` failure via
  `FailAsync` when a delta kind does not match the opened block kind.
- **Confidence:** High
- **Status:** Fixed ✅

### N09 — Bedrock streaming: `toolUse` delta on a text accumulator throws instead of failing typed

- **Package:** AgentKit.Providers.AwsBedrock
- **File:** `src/AgentKit.Providers.AwsBedrock/AwsBedrockResponseParser.cs:438`
- **Severity:** Low
- **Category:** correctness
- **Description:** A `contentBlockDelta` carrying `toolUse.input` for an index
  whose accumulator was opened by a text delta (or by a `contentBlockStart`
  without `toolUse`) reaches `HandleContentBlockDeltaAsync` with `Kind == Text`
  and `ToolCallId == null`; `accumulator.ToolCallId!.Value` throws
  `InvalidOperationException`, which is not caught by `AwsBedrockLlmModel` and
  escapes without a terminal observer event. The never-started case is handled
  (line 240), but the mismatched-kind case is not.
- **Evidence:**

  ```csharp
  else if (delta?.ToolUse?.Input is { Length: > 0 } jsonFragment)
  {
      _ = accumulator.Json.Append(jsonFragment);
      await observer.OnEventAsync(
          new ModelPartDelta(requestId, nextSequence(), index,
              new ToolArgumentsContentDelta(accumulator.ToolCallId!.Value, jsonFragment)), ...
  ```

- **Suggested fix:** Check `accumulator.Kind == BlockKind.ToolUse` before
  appending and surface a `ProtocolViolation` failure otherwise.
- **Confidence:** High
- **Status:** Fixed ✅

### N10 — Cohere `message-end` error text is parsed but discarded; stream errors surface as completed responses

- **Package:** AgentKit.Providers.Cohere
- **File:** `src/AgentKit.Providers.Cohere/CohereResponseParser.cs:289`
- **Severity:** Low
- **Category:** correctness
- **Description:** `CohereStreamEventDeltaDto.Error` is declared ("present only
  on `message-end`") but never read. When Cohere ends a stream with
  `finish_reason: "ERROR"` and an `error` message, the parser returns
  `ModelAttemptCompleted` with `NormalizedStopReason.Error` and drops the
  provider's error text entirely, so the attempt is not classified as a failure
  and no diagnostic evidence is retained. The Anthropic and Bedrock adapters map
  in-stream provider errors to `ModelAttemptFailed` with the provider message;
  this adapter is inconsistent and loses the only evidence of what went wrong.
- **Evidence:**

  ```csharp
  case "message-end":
      finishReason = streamEvent.Delta?.FinishReason;
      usage = streamEvent.Delta?.Usage ?? usage;
      sawMessageEnd = true;
      break;
  ```

- **Suggested fix:** When `Delta?.Error` is non-empty (or `finish_reason` is
  `ERROR`/`TIMEOUT`), return a `ModelAttemptFailed` via `FailAsync` carrying the
  error text as `ProviderErrorMessageEvidence` and the partial parts/usage.
- **Confidence:** High
- **Status:** Fixed ✅

### N11 — Gemini 429 `RESOURCE_EXHAUSTED` retry hint (`RetryInfo.retryDelay`) is not parsed

- **Package:** AgentKit.Providers.GoogleGemini
- **File:**
  `src/AgentKit.Providers.GoogleGemini/GoogleApiErrorFailureFactory.cs:56`
- **Severity:** Low
- **Category:** contract-mismatch
- **Description:** Google APIs communicate retry guidance for
  `RESOURCE_EXHAUSTED` through `error.details[]` entries of type
  `google.rpc.RetryInfo` with a `retryDelay` (e.g. `"20s"`), not through a
  `Retry-After` header. `GoogleGeminiErrorDetailDto` models only
  `code`/`message`/`status`, and `RetryAfterResolver` inspects headers only, so
  `ProviderFailure.RetryAfter` is null for essentially every Gemini/Vertex
  throttling response and retry policies lose the provider's explicit backoff
  instruction. Vertex AI (`GoogleVertexAILlmModel`) shares this factory.
- **Evidence:**

  ```csharp
  return new ProviderFailure(
      kind, providerId, requestId: null, (int) response.StatusCode, status,
      RetryAfterResolver.Resolve(response.Headers, timeProvider),
      $"The provider returned HTTP status {(int) response.StatusCode}.",
      diagnosticCause, ProviderErrorMessageEvidence.Create(providerMessage));
  ```

- **Suggested fix:** Add `details` to the error DTO, locate the
  `type.googleapis.com/google.rpc.RetryInfo` entry, parse its `retryDelay`
  duration string, and use it as `retryAfter` when no header is present.
- **Confidence:** High
- **Status:** Fixed ✅

### N12 — Default `HttpClient` registration keeps the 100 s `HttpClient.Timeout`, silently capping non-streaming attempts below the request deadline

- **Package:** AgentKit.Providers.Anthropic (identical registration in Cohere,
  MistralAI, AwsBedrock, GoogleGemini, GoogleVertexAI)
- **File:** `src/AgentKit.Providers.Anthropic/ServiceExtensions.cs:66`
- **Severity:** Low
- **Category:** correctness
- **Description:** Every package registers
  `TryAddSingleton(_ => new HttpClient())` without touching `Timeout`, so the
  BCL default of 100 seconds applies to `SendAsync`. With `ResponseHeadersRead`
  the timeout covers time-to-headers, which for `PreferStreaming = false`
  (Anthropic/Bedrock/Gemini buffered generation, and Bedrock `Converse` in
  particular) is the whole generation. A caller-supplied
  `LlmModelRequest.Deadline` longer than 100 s is therefore silently overridden
  and surfaces as "The transport timed out before the provider responded", while
  the adapter's own `deadlineSource` never fires. The adapters explicitly
  separate transport timeout from deadline in their catch clauses but the
  default composition makes the transport timeout the binding one.
- **Evidence:**

  ```csharp
  services.TryAddSingleton(TimeProvider.System);
  services.TryAddSingleton(_ => new HttpClient());
  ```

- **Suggested fix:** Register the default client with
  `Timeout = Timeout.InfiniteTimeSpan` (the per-request `deadlineSource` already
  bounds every attempt) or document that applications must configure the
  `HttpClient` timeout at or above their attempt deadlines.
- **Confidence:** Medium
- **Status:** Fixed ✅

### Areas reviewed (Group 06)

- `AgentKit.Providers.Anthropic`: `AnthropicMessageStreamParser` (buffered + SSE
  state machine, block accumulation, usage merge, error event, partial parts),
  `AnthropicMessageTranslator` (system hoisting, runtime envelope,
  tool_use/tool_result correlation, thinking/redacted_thinking, tool choice),
  `AnthropicLlmModel` (auth, deadline, `ResponseHeadersRead`, error body,
  request-id, retry-after), `AnthropicErrorMapping`, options/validator/defaults,
  `ServiceExtensions`.
- `AgentKit.Providers.Cohere`: `CohereResponseParser` (buffered +
  `*-start/-delta/-end` streaming, slot indexing), `CohereRequestTranslator`,
  `CohereLlmModel` transport/error path, `CohereErrorMapping`,
  `CohereEmbeddingResponseParser` (encodings, dimensions, billed units),
  `ServiceExtensions`.
- `AgentKit.Providers.MistralAI`: `MistralAIResponseParser` (buffered + `[DONE]`
  streaming, tool-call slots), `MistralAIRequestTranslator` +
  `MistralAIToolCallIdCodec` (nine-char wire-ID allocation and collision
  handling), `MistralAIEmbeddingResponseParser` (index correlation),
  `MistralAILlmModel` transport/error path, `ServiceExtensions`.
- `AgentKit.Providers.AwsBedrock`: `AwsEventStreamDecoder` + `Crc32` (framing,
  CRCs, bounds, headers), `AwsBedrockResponseParser` (buffered +
  ConverseStream), `AwsBedrockRequestTranslator`, `AwsSigV4Signer` (canonical
  request, double-encoding, query ordering, signing key), `AwsSigV4Credential`
  redaction, `AwsBedrockLlmModel` (signing, `x-amzn-errortype`,
  `x-amzn-RequestId`), `AwsBedrockErrorMapping`, `AwsBedrockProviderDefaults`
  URI building, `ServiceExtensions`.
- `AgentKit.Providers.GoogleGemini`: `GoogleGeminiResponseParser` (buffered +
  `alt=sse` streaming, thought/thoughtSignature routing, synthesized tool-call
  IDs), `GoogleGeminiContentTranslator` (systemInstruction,
  functionCall/functionResponse ids, thought signatures, tool config),
  `GoogleApiErrorFailureFactory`, `GoogleGeminiErrorMapping`,
  `GoogleGeminiThoughtSignature`, embedding translator/parser,
  `ServiceExtensions`.
- `AgentKit.Providers.GoogleVertexAI`: `GoogleVertexAIProviderDefaults`
  (global/regional routing, project/location/publisher/endpoint resources;
  verified Vertex v1 `FunctionCall.id` exists), options/validator,
  `GoogleVertexAILlmModel` transport path, embedding translator/parser,
  `ServiceExtensions`.
- Shared: `ServerSentEventReader`, `ProviderToolCallIds`, `ProviderJson`,
  `ProviderAuthorizationHeaderFactory`/`ProviderAuthorizationGranted`,
  `RetryAfterResolver`, `HttpStatusFailureKindMapper`, `ModelRequestPreflight`,
  `EmbeddingSpaceIdentity` contract, `DefaultAgentLoop` tool-message
  construction (for message-shape assumptions).

### Areas not reviewed (Group 06)

- Live-service behaviour (no network calls to providers); provider API claims
  are based on public documentation and the repository's own fixtures/tests.
- `DelegatingOAuthCredentialSource`, `SequencingModelResponseObserver`,
  `ProviderRequestIdReader`, `ProviderErrorMessageEvidence`,
  `RuntimeMessageProjection` internals (shared/out of group scope; only their
  call sites were inspected).
- Cohere rerank (no rerank implementation exists in
  `AgentKit.Providers.Cohere`), Cohere/Mistral/Gemini/Vertex embedding `*Model`
  transport classes beyond spot checks (they mirror the LLM model pattern), and
  the `KnownModelCatalog` resources.
- Test projects were consulted for intent (README, fixtures, selected
  parser/signer/defaults tests) but not exhaustively read; observability
  (`ProviderLog`/`ProviderMetrics`) coverage for these adapters was not
  assessed.

## Group 07 — Permissions and Budgets (with InMemory/Sqlite stores)

Read-only review of `AgentKit.Permissions`, `AgentKit.Permissions.InMemory`,
`AgentKit.Permissions.Sqlite`, `AgentKit.Budgets`, `AgentKit.Budgets.InMemory`,
`AgentKit.Budgets.Sqlite`, and `AgentKit.Budgets.Storage.Shared`, cross-checked
against the contracts in `AgentKit.Abstractions/Security` and
`AgentKit.Abstractions/Budgets`, the mirror test projects, the conformance
suites, and the normative concept documents. The grant issuance → validation →
once-only consumption path is sound in both stores (per-grant monitor + global
intent lock in memory; `BEGIN IMMEDIATE` read-modify-write in SQLite; exclusive
expiry via `TimeProvider`; exact-evidence and revocation checks before
decrement). Policy evaluation fails closed (zero allows → deny, policy crash →
deny, approval unavailable → deny). Budget reservations are all-or-nothing in
both ledgers, arithmetic is exact via `BudgetQuantity`/`BigInteger`, and SQLite
writes are single immediate transactions with digest-verified payloads. Four
defects were found: **0 High, 3 Medium, 1 Low**. The Medium items are (a) a
normative audit-coverage gap in the security authority and (b) a hold-clearance
/ hard-failure accounting inconsistency, present identically in both ledger
adapters, where expired-but-unswept unstarted reservations still count as
retained capacity and can leave a scope stuck in `Held`.

### B01 — SecurityAuthority emits no audit record for requests, decisions, denials, or non-approval grants

- **Package:** AgentKit.Permissions
- **File:** `src/AgentKit.Permissions/SecurityAuthority.cs:317`
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** `docs/concepts/permissions-approvals-and-trust.md` ("Audit")
  states that every request, winning policy, decision, approval transition,
  grant issue and consumption, denial, expiry, and revocation MUST emit a
  redacted audit record, and that required audit persistence is part of
  settlement. The authority only dispatches one `GrantIssued` record, and only
  when a policy returned `RequireApproval`; the ordinary allow path
  (`AllowAllSecurityPolicy`, `WorkspaceScopedFileAccessPolicy`) registers a
  grant with no audit at all, and every `Denied(...)` return path is silent.
  Neither grant store audits consumption or `RevokeAsync`. With
  `AuditDelivery = Required`, a host that believes denials and non-approval
  grants are being durably audited gets no records, and the fail-closed
  "required audit unavailable → deny" rule is never exercised for those paths.
  The `ISecurityAuditDispatcher` dependency is only wired into the
  approval-capable constructor, so the default composition (no `IApprovalStore`)
  cannot audit even if asked.
- **Evidence:**

  ````csharp
  if (approvalRequestId is { } approvedRequestId)
  {
      var audit = new SecurityAuditRecord(
          new SecurityAuditRecordId(approvedRequestId.Value),
          ...
          SecurityAuditEventKind.GrantIssued,
          SecurityAuditOutcome.Accepted,
  ...
  await _grantStore.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
  return new SecurityAllowed(request.Id, policyVersion, grant);
  ```text
  and every `return Denied(request, policyVersion, "security.no_policy", ...)` /
  `"security.policy_evaluation_failed"` / `"security.deadline_expired"` path
  performs no dispatch.
  ````

- **Suggested fix:** Make `ISecurityAuditDispatcher` a required dependency of
  both constructors and dispatch `Decision` (Accepted/Denied) records for every
  terminal decision and `GrantIssued` for every registered grant, denying when
  required delivery is not accepted; have the grant stores (or the authority on
  their behalf) emit `GrantLifecycle` on revoke.
- **Confidence:** High
- **Status:** Fixed ✅

### B02 — In-memory hold clearance and hard-failure checks count expired-but-unswept unstarted reservations as retained capacity

- **Package:** AgentKit.Budgets.InMemory
- **File:** `src/AgentKit.Budgets.InMemory/InMemoryBudgetLedger.cs:1036`
- **Severity:** Medium
- **Category:** correctness
- **Description:** `ReserveBatchCoreAsync` and `GetSnapshotCoreAsync` treat an
  unstarted reservation whose `EffectiveReservation.ExpiresAt` has passed as
  released (they compute `FindExpired(now)` and pass it to
  `Usage(..., expired)`), which matches the concept document ("Expiry and
  disposal release an unstarted reservation"). `EligibleForClear` (used by
  `CorrectCoreAsync`) and `CurrentHardFailures` (used by
  `ResolveOverrunHoldCoreAsync`) do not: `EligibleForClear` sums every
  `IsCapacityRetaining` row and `CurrentHardFailures` calls
  `Usage(boundary, dimension)` with no exclusion. Because expiry is only
  materialised lazily by reserve/snapshot/mark-started, a correction that brings
  the only overrun row back within the hard ceiling can be judged "not eligible"
  purely because a dead reservation inflates `observed`, so the
  `ClearWhenReconciled` hold stays active and every subsequent
  `ReserveBatchAsync` returns `Held` (the hold check runs before the expiry
  sweep). Nothing except another correction on that dimension re-evaluates
  clearance, so the scope is stuck until an operator issues a synthetic
  correction. Likewise `ResolveOverrunHoldAsync` can return `Blocked` with a
  fabricated hard failure and, because the result is persisted under the
  idempotency key, the key is burned. Example: hard limit 100 (Sum); R1 = 60
  unstarted and expired; R2 reserved 45, settled 50 (hold); correct R2 → 45:
  observed = 60 + 45 = 105 > 100 → hold not cleared although real usage is 45.
- **Evidence:**

  ````csharp
  var retainedValues = boundary.Reservations
      .Where(item => item.IsCapacityRetaining && item.Receipt.OriginalRequest.Dimension == original.Dimension)
      .Select(item => item.Receipt.OriginalRequest.Amount)
      .ToArray();
  ...
  // CurrentHardFailures
  var (reserved, committed) = Usage(boundary, dimension);
  ```text
  versus the admission path:
  `var expired = FindExpired(now); ... var (Reserved, Committed) = Usage(boundary, group.Key.Dimension, expired);`
  ````

- **Suggested fix:** Compute `FindExpired(_timeProvider.GetUtcNow())` in
  `CorrectCoreAsync` and `ResolveOverrunHoldCoreAsync` and pass it (or
  sweep/expire first, with revision capacity reserved) so `EligibleForClear` and
  `CurrentHardFailures` see the same retained set as
  `ReserveBatch`/`GetSnapshot`; add a conformance test covering an expired
  unstarted row at correction/resolution time.
- **Confidence:** High
- **Status:** Fixed ✅

### B03 — SQLite hold clearance and hard-failure checks read `projection.Reserved`, which still includes expired-but-unswept unstarted reservations

- **Package:** AgentKit.Budgets.Sqlite
- **File:** `src/AgentKit.Budgets.Sqlite/SqliteBudgetLedger.cs:673`
- **Severity:** Medium
- **Category:** correctness
- **Description:** Same defect as B02 in the durable adapter, so it is not a
  store-specific divergence but a shared semantic gap. `ReserveBatchCore` (lines
  850–882) and `SnapshotCore` (lines 1176–1196) subtract expired rows from the
  persisted projection (or recompute `MaximumRetained` excluding them) before
  comparing with limits, but `EligibleForClear` (line 673) and
  `CurrentHardFailures` (line 1437) use `projection.Reserved` unadjusted.
  `budget_dimension_projections.reserved_coefficient` is only decremented when
  `PersistExpiration` runs, which happens exclusively inside
  reserve/snapshot/mark-started. The consequences are identical: a
  `ClearWhenReconciled` hold that should clear stays active and blocks the
  scope; `ResolveOverrunHoldAsync` returns `Blocked` with a hard failure that
  the admission path would not report, and the `Blocked` result is persisted
  under the operator's idempotency key.
- **Evidence:**

  ````csharp
  var projection = ReadProjection(connection, transaction, boundary.Reference.Id, dimension)
      ?? throw new InvalidDataException("Settled accounting has no dimension projection.");
  ...
  BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => projection.Reserved.Add(committed),
  ...
  return observed.CompareTo(BudgetQuantity.FromDecimal(hard.Value)) <= 0;
  ```text
  while `ReserveBatchCore` does
  `reserved = Subtract(reserved, expiredRow.Receipt.OriginalRequest.Amount);`
  for each expired row before the same comparison.
  ````

- **Suggested fix:** In `CorrectCore` and `ResolveOverrunHoldCore`, load the
  boundary's capacity rows, compute the expired set with the ledger clock, and
  either persist their expiration inside the same transaction before evaluating,
  or subtract them from `projection.Reserved` / recompute `MaximumRetained`
  exactly as `ReserveBatchCore` does.
- **Confidence:** High
- **Status:** Fixed ✅

### B04 — Approval broker never bounds the handler wait by the approval binding's expiry

- **Package:** AgentKit.Permissions
- **File:** `src/AgentKit.Permissions/DefaultApprovalBroker.cs:102`
- **Severity:** Low
- **Category:** async
- **Description:** `SecurityAuthority` computes
  `approvalExpiry = min(request.Deadline, now + MaximumGrantLifetime)` and
  stores it in `ApprovalScopeBinding.ExpiresAt` precisely so an approval cannot
  be honoured after that instant, and the broker checks `IsExpired(request)`
  after the handler returns. However the broker awaits
  `_handler.TryResolveAsync(request, cancellationToken)` with only the caller's
  token, and it owns a `TimeProvider` it does not use for a deadline. An
  `IApprovalHandler` that waits for a human (the expected production shape)
  therefore blocks the authorization — and the tool call/run behind it —
  indefinitely past `Binding.ExpiresAt` unless the caller independently cancels;
  when it finally returns the broker discards the answer as
  `ApprovalBrokerExpired`. The deadline is enforced only for correctness, never
  for liveness.
- **Evidence:**

  ```csharp
  ApprovalHandlerResult handlerResult;
  try
  {
      handlerResult = await _handler.TryResolveAsync(request, cancellationToken).ConfigureAwait(false);
  }
  ...
  if (IsExpired(request) || response.RespondedAt >= request.Binding.ExpiresAt)
  {
      return new ApprovalBrokerExpired();
  }
  ```

- **Suggested fix:** Create a `CancellationTokenSource` from `_timeProvider`
  scheduled at `request.Binding.ExpiresAt - now`, link it with
  `cancellationToken`, pass the linked token to the handler, and map handler
  cancellation caused by the deadline to `ApprovalBrokerExpired` while still
  propagating caller cancellation.
- **Confidence:** Medium
- **Status:** Fixed ✅

### Areas reviewed (Group 07)

- `SecurityAuthority` end to end: captured-context check, deadline, additive
  policy loop with deny precedence and crash-to-deny, approval binding
  construction, post-approval staleness checks, grant lifetime/uses clamping,
  required approval audit, grant registration.
- `DefaultApprovalBroker`: capability gate, create/read/replay, request-id and
  binding equality, tenant check, responder authorization, resolve conflict
  handling, terminal audit.
- `DefaultSecurityAuditDispatcher`: required/best-effort classification,
  durable-acceptance requirement, per-sink `TimeProvider` deadline, cancellation
  vs timeout vs failure catch ordering, late-completion observation (confirmed
  intentional by tests).
- `InMemorySecurityGrantStore` and `SqliteSecurityGrantStore`: register/replay,
  legacy and intent consumption ordering (tamper → receipt replay → revocation →
  validity window → effect match → exhaustion → decrement), lock discipline,
  `BEGIN IMMEDIATE`, receipt persistence, revocation, digest verification, codec
  bounds, schema validation, exception mapping.
- `InMemoryApprovalStore`, `DefaultSecurityProfileSelector`,
  `DefaultSecurityAuthoritySelector`, `DefaultSecurityProfilePublicationReader`,
  `WorkspaceScopedFileAccessPolicy`, `AllowAllSecurityPolicy`,
  `DenyAllSecurityAuthority`, DI registrations and options validation.
- `BudgetAuthority`, `BudgetScope`, `BudgetReservation` (dispose retains started
  reservations), `BudgetQuantity` exact arithmetic,
  `BudgetScopeCreateTransition`.
- `InMemoryBudgetLedger` and `SqliteBudgetLedger` side by side: scope
  creation/replay, batch reservation atomicity and replay/conflict, hold check,
  expiry sweep, open-capacity bound, hard-limit projection for
  Sum/Duration/ConcurrentGauge/Maximum, mark-started/expiry replay,
  settle/replay/conflict, release semantics, corrections and hold
  creation/clearing, snapshots, unresolved-started paging and cursor validation,
  reconciliation idempotency, overrun-hold resolution, revision accounting.
- SQLite schema (types/affinity, PKs, CHECKs, indexes), transaction wrapper and
  acknowledgement classification, `budget_maximum_values` sortable-key
  maintenance, quantity BLOB encoding, reader disposal on all paths.
- Conformance suites for `ISecurityGrantStore`, `IApprovalStore`,
  `ISecurityAuditDispatcher`, and `IBudgetLedger`, plus package unit tests, to
  rule out intentional behaviour.

### Areas not reviewed (Group 07)

- `SecurityCanonicalFingerprint` / `SecurityEnforcementFingerprintPayload`
  canonicalisation internals (Abstractions).
- `BudgetOverrunSecurityBinding.Matches` and the effecting-boundary side of
  operator hold resolution (outside this group).
- Session directory audit emission (`AgentKit.Session.*`) that references
  `SecurityAuditEventKind`.
- Metrics/log source-generator files beyond checking that templates carry only
  identifiers.
- Cross-process SQLite behaviour exercised by
  `AgentKit.Budgets.Sqlite.ProcessHost` (not executed; static review only).
- `AgentKit.Permissions.Sqlite` codec writer/reader exhaustive bound tests
  (skimmed, not line-by-line).

## Group 08 — FileSystem, Network, Processes, LanguageServices

Read-only review of the low-level host boundaries (`AgentKit.FileSystem`,
`AgentKit.FileSystem.InMemory`, `AgentKit.Network`, `AgentKit.Network.InMemory`,
`AgentKit.Processes`, `AgentKit.Processes.Scripted`,
`AgentKit.LanguageServices.Scripted`) against the abstractions, the normative
concept specs, and the mirror tests. The real filesystem adapter is built on
`openat`/`O_NOFOLLOW` descriptor walking rather than `GetFullPath`+`StartsWith`,
so the classic string-confinement bugs are absent; the defects found are instead
in raw libc struct-layout assumptions, non-atomic `CreateOrOverwrite`, blocking
opens on special files, an unbounded stdin write in the process runner, HTTP
connection-pool reuse that ignores the pinned peer, and an in-memory adapter
path bug. Totals: **2 High, 5 Medium, 3 Low** (10 findings).

### H01 — Raw libc `fstat`/`readdir` struct offsets are only correct for x86_64 Linux and arm64 macOS (wrong on x86_64 macOS and aarch64 Linux)

- **Package:** AgentKit.FileSystem
- **File:** `src/AgentKit.FileSystem/SandboxedFileSystem.Edit.cs:302` (also
  `src/AgentKit.FileSystem/SandboxedFileSystem.cs:777`)
- **Severity:** High
- **Category:** correctness
- **Description:** `TryGetFileMode` reads `st_mode` at a hard-coded offset (`4`
  on macOS, `24` elsewhere) and `TryReadDirectoryNames` reads `d_name` at offset
  `21`/`19`. On aarch64 Linux `struct stat` places `st_mode` at offset 16 and
  `st_uid` at offset 24, so the "preserved mode" applied via
  `fchmod(staging, mode & 0xFFF)` in `ReplaceExisting` writes, `ReplaceAsync`,
  and patch replaces is actually the owner UID (e.g. uid 0 → mode 0000, uid 1000
  → 01750). On x86_64 macOS,
  `LibraryImport("libc", EntryPoint = "fstat"/"readdir"/"fdopendir")` binds the
  legacy 32-bit-inode symbols (the 64-bit ones are `fstat$INODE64`,
  `readdir$INODE64`, `fdopendir$INODE64`), whose layouts put `st_mode` at offset
  8 and `d_name` at offset 8; reading `d_name` at 21 yields truncated/empty
  names, and an empty name reaches `new FileSystemPath("")` in
  `EnumerateCoreAsync`, which throws `ArgumentException` out of the public API.
  Enumerate/Glob/Search are therefore broken on Intel Macs and file permissions
  are silently corrupted on arm64 Linux containers.
- **Evidence:**

```csharp
mode = OperatingSystem.IsMacOS()
    ? Marshal.ReadInt16(buffer, 4) & 0xFFFF
    : Marshal.ReadInt32(buffer, 24);
...
var nameOffset = OperatingSystem.IsMacOS() ? 21 : 19;
var name = Marshal.PtrToStringUTF8(IntPtr.Add(entry, nameOffset));
```

- **Suggested fix:** Replace the raw `fstat` P/Invoke with
  `File.GetUnixFileMode(SafeFileHandle)` and the raw `readdir` loop with a
  per-architecture layout (or `Directory.EnumerateFileSystemEntries` over a
  handle-derived path only where safe); on macOS x86_64 bind the `$INODE64`
  entry points explicitly, and add
  `RuntimeInformation.ProcessArchitecture`-gated tests for the offsets.
- **Confidence:** High
- **Status:** Fixed ✅

### H02 — `CreateOrOverwrite` on an existing file truncates in place; not atomic and leaves an empty/partial target on failure or cancellation

- **Package:** AgentKit.FileSystem
- **File:** `src/AgentKit.FileSystem/SandboxedFileSystem.cs:1007`
- **Severity:** High
- **Category:** contract-mismatch
- **Description:** `docs/architecture/file-system.md` (§write dispositions) and
  `docs/concepts/file-system-access-and-bounds.md` require `CreateOrReplace` on
  an existing regular file to "atomically replace" by staging outside the
  visible target so that "limit, cancellation, encoding, or fingerprint failure
  before that commit MUST leave the prior target unchanged". `WriteCoreAsync`
  implements the existing-target branch by reopening with `O_WRONLY|O_TRUNC`
  (`ExistingFileOpenFlags`) and then writing into the truncated file. A
  cancellation observed by `FileStream.WriteAsync` (the token is re-checked
  there after the last `ThrowIfCancellationRequested`), an `ENOSPC`/`EIO` during
  the write, or process death between open and write leaves the target
  zero-length or partially written, whereas `ReplaceExisting` in the same method
  correctly stages and `renameat`s.
- **Evidence:**

```csharp
private static int ExistingFileOpenFlags(FileWriteMode mode) => mode == FileWriteMode.CreateOrOverwrite
    ? _openWriteOnly | TruncateFlag | NoFollowFlag | CloseOnExecFlag
    : throw new UnreachableException();
...
descriptor = OpenAt(parentDescriptor, fileName, ExistingFileOpenFlags(mode), 0);
```

- **Suggested fix:** When the `O_CREAT|O_EXCL` attempt fails with `EEXIST`,
  route `CreateOrOverwrite` through the same staged `ReplaceExistingWriteAsync`
  path (stage with `O_EXCL`, fsync, `renameat`) instead of reopening with
  `O_TRUNC`.
- **Confidence:** High
- **Status:** Fixed ✅

### H03 — `ReadAsync`/`WriteAsync` open the target without `O_NONBLOCK`, so a FIFO inside the workspace hangs the operation uncancellably

- **Package:** AgentKit.FileSystem
- **File:** `src/AgentKit.FileSystem/SandboxedFileSystem.cs:176`
- **Severity:** Medium
- **Category:** async
- **Description:** Snapshot, search, replace, and patch all open candidate files
  with `NonBlockingFlag` (and the test suite explicitly covers named pipes for
  those paths), but `ReadCoreAsync` opens with `O_RDONLY|O_NOFOLLOW` only and
  `NewFileOpenFlags`/`ExistingFileOpenFlags` open with `O_WRONLY` only.
  `open(2)` on a FIFO with no peer blocks inside the native call, so a FIFO
  created in the workspace (e.g. by a sandboxed process with `mkfifo`) makes
  `ReadAsync`/`WriteAsync` block forever on a thread-pool thread; the
  `CancellationToken` cannot interrupt a blocking P/Invoke, and the grant has
  already been consumed.
- **Evidence:**

```csharp
var descriptor = OpenAt(
    parent.DangerousGetHandle().ToInt32(),
    fileName,
    _openReadOnly | NoFollowFlag | CloseOnExecFlag,
    0);
```

- **Suggested fix:** Add `NonBlockingFlag` to the read and write opens (as the
  other paths already do), then `fstat` the descriptor and refuse anything that
  is not `S_IFREG` with a typed failure before reading or writing.
- **Confidence:** High
- **Status:** Fixed ✅

### H04 — Standard-input delivery is not covered by the operation timeout; a child that never reads stdin hangs the run indefinitely

- **Package:** AgentKit.Processes
- **File:** `src/AgentKit.Processes/OperatingSystemProcessRunner.cs:222`
- **Severity:** Medium
- **Category:** async
- **Description:** `RunCreatedProcessAsync` awaits `WriteInputAsync` with only
  the caller's token, and the
  `CancellationTokenSource(intent.Request.Limits.Timeout, _timeProvider)` is
  created afterwards (line 244). `MaximumInputBytes` defaults to 1 MiB while a
  Unix pipe buffer is 64 KiB, so a child that does not consume stdin (e.g. a
  long-running tool) blocks the write after the first 64 KiB and the declared
  `Limits.Timeout` never fires; the process is neither terminated nor reported
  as `TimedOut` until the caller cancels externally. This defeats the
  bounded-execution guarantee of the process contract.
- **Evidence:**

```csharp
try
{
    await WriteInputAsync(process, intent.Request.StandardInput, cancellationToken).ConfigureAwait(false);
}
...
using var timeout = new CancellationTokenSource(intent.Request.Limits.Timeout, _timeProvider);
using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
```

- **Suggested fix:** Create the timeout/linked CTS before starting the process
  and pass `operation.Token` to `WriteInputAsync` (mapping timeout cancellation
  to `TimedOut`), or run the stdin write concurrently with `WaitForExitAsync`
  under the same linked token.
- **Confidence:** High
- **Status:** Fixed ✅

### H05 — Pooled HTTP connections are keyed by host:port, so a later request can be served over a connection to an address that is not the request's pinned, still-valid resolved address

- **Package:** AgentKit.Network
- **File:** `src/AgentKit.Network/DefaultNetworkTransport.cs:59`
- **Severity:** Medium
- **Category:** security
- **Description:** The transport pins the authorized IP only through
  `ConnectCallback`, which `SocketsHttpHandler` invokes solely when it opens a
  new connection; the pool key is (scheme, host, port, SNI host), not the peer
  address. A second send to the same origin whose grant binds a different (or
  freshly re-resolved) address reuses the idle connection to the first address
  for up to `PooledConnectionIdleTimeout` (default 1 min,
  `PooledConnectionLifetime` infinite). `docs/architecture/network.md` §"Request
  evidence and connection reuse" and the concept spec require the pool to be
  partitioned by peer address and the actual peer to satisfy each request's
  grant "including connection reuse and multiplexing"; the implementation does
  not verify that.
- **Evidence:**

```csharp
_invoker = new HttpMessageInvoker(
    new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        MaxResponseHeadersLength = options.Value.MaximumResponseHeaderKilobytes,
        ConnectCallback = ConnectAsync,
    },
    disposeHandler: true);
```

- **Suggested fix:** Partition the pool by resolved address (e.g. one handler
  per pinned address, or set `ConnectionClose = true` /
  `PooledConnectionLifetime = TimeSpan.Zero`), or verify `RemoteEndPoint` of the
  connection against the request's address before each send.
- **Confidence:** High
- **Status:** Fixed ✅

### H06 — Caller-supplied `Host` header is forwarded verbatim, overriding the HTTP virtual host and TLS SNI/certificate target without re-enforcement against the destination allow-list

- **Package:** AgentKit.Network
- **File:** `src/AgentKit.Network/DefaultNetworkTransport.cs:199`
- **Severity:** Medium
- **Category:** security
- **Description:** `BuildMessage` copies every `NetworkHeader` with
  `TryAddWithoutValidation`, including `Host`. `SocketsHttpHandler` uses
  `request.Headers.Host` both as the wire `Host` header and as the
  SNI/certificate validation name
  (`HttpConnectionPoolManager.GetConnectionKey`). The transport's own re-check
  (`_policy.AllowsSchemeAndHost(request.Destination)`) only inspects
  `Destination.Host`, so a request authorized for an allow-listed host can reach
  a different virtual host on the same pinned IP (shared CDN/ingress, internal
  name on a dual-homed front end) with TLS validated for that other name. Per
  AGENTS.md the low-level boundary must re-enforce the concrete effect rather
  than rely on the higher-level policy having inspected free-form headers.
- **Evidence:**

```csharp
foreach (var header in request.Headers.Headers)
{
    _ = message.Headers.TryAddWithoutValidation(header.Name, header.Value);
}
```

- **Suggested fix:** Reject (or deny with `NetworkDenied`) requests carrying
  `Host`, and consider a small deny-list for other connection-controlling
  headers (`:authority`, `Proxy-*`, `Upgrade`, `Connection`), so the destination
  identity in the grant is the only authority for host/SNI.
- **Confidence:** Medium
- **Status:** Fixed ✅

### H07 — In-memory Glob/Search with a non-null `BasePath` double-prefix result paths and match patterns/exclusions against the full workspace path instead of the base-relative path

- **Package:** AgentKit.FileSystem.InMemory
- **File:** `src/AgentKit.FileSystem.InMemory/InMemoryFileSystem.Glob.cs:97`
  (also `src/AgentKit.FileSystem.InMemory/InMemoryFileSystem.Search.cs:183`)
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** `TraverseGlobDirectory`/`TraverseSearchDirectory` start from
  `basePath` and build `relative = "{directoryPath}/{name}"`, which is already
  the full workspace path (it is what `_files`/`_directories` are keyed by). The
  real adapter matches `Pattern`, `PathPattern`, and `ExcludedPathPatterns`
  against the base-relative path and then prefixes `BasePath` once. The
  in-memory adapter matches against the base-prefixed path (so `*.cs` under
  `BasePath=src` matches nothing while `src/*.cs` matches) and then prefixes
  `BasePath` again, producing paths like `src/src/a.cs`. The two adapters
  therefore disagree on the same request; no in-memory test exercises a non-null
  `BasePath` for glob or a matching search.
- **Evidence:**

```csharp
var relative = directoryPath is null ? name : $"{directoryPath}/{name}";
...
var workspacePath = state.Request.BasePath is null ? relative : $"{state.Request.BasePath.Value.Value}/{relative}";
if (GlobMatches(state.Request.Pattern.Value, relative, state.Request.CaseSensitive))
```

- **Suggested fix:** Track the base-relative path separately (strip
  `basePath + "/"` from `relative` or carry a `relativeToBase` parameter), match
  patterns/exclusions on it, and use the existing full path as the result; add
  shared conformance tests with a non-null `BasePath` run against both adapters.
- **Confidence:** High
- **Status:** Fixed ✅

### H08 — Whitespace-only entry names make Enumerate/Glob/Search throw `ArgumentException` instead of returning a typed failure

- **Package:** AgentKit.FileSystem
- **File:** `src/AgentKit.FileSystem/SandboxedFileSystem.cs:498`
- **Severity:** Low
- **Category:** validation
- **Description:** `FileSystemPath`'s constructor rejects whitespace-only
  values. `EnumerateCoreAsync` (`new FileSystemPath(path)` in the `Select`),
  `GlobCoreAsync` (`state.Matches.Select(new FileSystemPath)`), and
  `SearchFileAsync` (`new FileSystemPath(workspacePath)`) construct paths from
  host entry names, so a top-level file or directory named `" "` or `"\t"`
  (creatable by any sandboxed process) escapes the
  `IOException`/`UnauthorizedAccessException` filters and surfaces as an
  unhandled `ArgumentException` after the grant was consumed. The backslash case
  is handled explicitly (`DirectoryEnumerationStatus.Failed`), but the
  whitespace case is not.
- **Evidence:**

```csharp
var retained = childPaths.Skip(start).Take(request.MaximumEntries)
    .Select(static path => new DirectoryEntry(new FileSystemPath(path)))
    .ToImmutableArray();
```

- **Suggested fix:** Treat names that `FileSystemPath` cannot represent
  (whitespace-only, NUL) the same way as backslash names: return the existing
  "cannot be represented by this path profile" failure (or skip in glob/search)
  before constructing the value.
- **Confidence:** High
- **Status:** Fixed ✅

### H09 — A zero `TerminationGracePeriod` makes the post-exit output drain race with EOF and report a clean exit as `Failed`

- **Package:** AgentKit.Processes
- **File:** `src/AgentKit.Processes/OperatingSystemProcessRunner.cs:374`
- **Severity:** Low
- **Category:** concurrency
- **Description:** `ProcessResourceLimits` allows
  `TerminationGracePeriod == TimeSpan.Zero` and the runner reuses it as the
  drain bound after a normal exit. `Task.Delay(TimeSpan.Zero, ...)` is already
  complete, so if the pipe readers have not yet observed EOF at the instant
  `WaitForExitAsync` completes (SIGCHLD reaping typically precedes the reader
  continuation), `Task.WhenAny` picks the delay, the tree is killed, streams are
  closed, and the run is settled as `ProcessRunStatus.Failed` with a null exit
  code and `MayHaveOccurred` even though the process exited normally.
  `WaitForExitWithinAsync` special-cases zero but `DrainOutputAsync` does not.
- **Evidence:**

```csharp
var delay = Task.Delay(drainBound, _timeProvider, CancellationToken.None);
if (await Task.WhenAny(both, delay).ConfigureAwait(false) == both)
{
```

- **Suggested fix:** Use a separate, minimum non-zero drain bound (or
  `ForcedTerminationWait`) for the post-exit drain, or require
  `TerminationGracePeriod > 0` at the resolver boundary.
- **Confidence:** Medium
- **Status:** Fixed ✅

### H10 — Raw `kill(pid, SIGKILL)` after the tree kill can target a reused PID once the child has been reaped

- **Package:** AgentKit.Processes
- **File:** `src/AgentKit.Processes/OperatingSystemProcessRunner.cs:408`
- **Severity:** Low
- **Category:** concurrency
- **Description:** `TerminateAsync` calls `TryKillTree` (which SIGKILLs the root
  if it has not exited) and then unconditionally issues a second raw
  `Kill(process.Id, 9)`. .NET's SIGCHLD handler reaps exited children
  immediately, so if the process dies between those two calls the PID is free
  and the raw signal can hit an unrelated process; `Process.Kill` avoids this by
  checking exit state under the wait-state lock, which the direct P/Invoke
  bypasses. The same window exists for the initial
  `Kill(process.Id, _signalTerminate)` after the unsynchronized `HasExited`
  check.
- **Evidence:**

```csharp
_ = TryKillTree(process);
if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
{
    _ = Kill(process.Id, 9);
}
```

- **Suggested fix:** Drop the redundant raw SIGKILL (rely on
  `Process.Kill(entireProcessTree: true)`), and for SIGTERM either use a process
  group (`setsid` + `kill(-pgid)`) or accept the narrow window and at least
  re-check `HasExited` immediately before signalling.
- **Confidence:** Medium
- **Status:** Fixed ✅

### Areas reviewed (Group 08)

- `SandboxedFileSystem` path resolution (`TryOpenParentDirectory`,
  `TryOpenDirectory`), `FileSystemPath` invariants, `O_NOFOLLOW`/`O_DIRECTORY`
  walking, root handling.
- Read path (`ReadCoreAsync`, `ReadSnapshotFromHandleAsync`), byte bounds,
  growth detection.
- Write dispositions (`TryOpenWriteTarget`, `NewFileOpenFlags`,
  `ExistingFileOpenFlags`, `ReplaceExistingWriteAsync`), staging/rename/fsync,
  mode preservation, parent-directory behavior.
- `ReplaceAsync` (mutation locks, fingerprint precondition), `ApplyPatchAsync`
  (structure validation, grant validation order, lock ordering,
  preflight/stage/revalidate/commit, `renameat2`/`renameatx_np`, cleanup).
- Traversal: `EnumerateAsync`, `GlobAsync`, `SearchAsync`
  (hidden/exclusion/depth/visit/byte/match/duration bounds, symlink/ELOOP
  handling, UTF-8 line projection, regex engine pinning), `readdir` marshalling.
- Grant re-enforcement (`FileSystemEnforcementReceipt`, `FileSecurityBinding`,
  `ProcessSecurityBinding`, `NetworkSecurityBinding`) and fingerprint inputs.
- `InMemoryFileSystem` (read/write/enumerate/glob/search/replace/patch) parity
  with the real adapter.
- `OperatingSystemProcessIntentResolver` (realpath canonicalization, allow-list,
  working-directory confinement, environment allow-list, bounds, executable
  fingerprint), `OperatingSystemProcessRunner` (start info, `ArgumentList`, env
  clearing, stdin/stdout/stderr pumping, timeout, termination, drain, output
  tails/artifacts), `PlatformProcessSandboxProvider` (sandbox-exec profile
  escaping/contents, bwrap arguments, fail-closed availability),
  `BoundedByteTail`/`BoundedByteCapture`, `ScriptedProcessRunner`.
- `DefaultNetworkNameResolver`, `NetworkDestinationPolicy`
  (private/loopback/mapped ranges), `DefaultNetworkTransport` (pinned connect,
  redirect non-following, declared/streamed size bounds, deadlines, header
  forwarding), `BoundedReadStream`, `RealNetworkResponse`,
  `ScriptedNetworkTransport`.
- `ScriptedLanguageIntelligenceService`.
- Mirror tests for FileSystem, FileSystem.InMemory, Processes, Network;
  `docs/concepts` file-system/network/process specs and
  `docs/architecture/file-system.md`, `network.md` sections.

### Areas not reviewed (Group 08)

- Observability partial classes beyond the wrapper flow (log event IDs, metric
  tags) and `ServiceExtensions` registration idempotency details.
- `ScriptedProcessIntentResolver`, `ScriptedNetworkNameResolver` internals, and
  the LanguageServices enforcement receipt beyond the main query path.
- Behavior on Windows (all real adapters fail closed with "unavailable on this
  platform"; not exercised).
- `ISecurityGrantStore` implementations (out of scope; assumed to enforce
  resource/effect matching).
- Runtime execution on x86_64 macOS / aarch64 Linux to empirically confirm H01
  (analysis is from documented struct layouts and symbol conventions).

## Group 09 — AgentKit facade, Simple, Hooks, Identity, Observability, Context, Goals, Artifacts

Read-only review of `src/AgentKit`, `src/AgentKit.Simple`, `src/AgentKit.Hooks`,
`src/AgentKit.Identity`, `src/AgentKit.Observability`, `src/AgentKit.Context`,
`src/AgentKit.Goals`, `src/AgentKit.Artifacts`, and
`src/AgentKit.Artifacts.InMemory`, cross-checked against their mirror tests and
the invariants in `AGENTS.md`. The facade's ownership/disposal model, admission
revalidation, catalog precedence, hook ordering (Kahn's algorithm with
registration-index tie-break, cycle/anchor/duplicate detection), identity
delegation narrowing, and the in-memory artifact store's tenant partitioning and
grant consumption are sound and well-tested. The defects found are concentrated
in three areas: composition validation that stops short of the run-time
collaborator bundle, observability calls that are allowed to alter control flow
(contrary to the repository's own invariant and to the guarded patterns used
elsewhere in the same packages), and a few contract/determinism gaps. Counts:
**High: 0, Medium: 4, Low: 7** (11 findings).

---

### F01 — Composition validation does not verify the per-run collaborator bundle; missing services surface only inside `RunAsync`

- **Package:** AgentKit
- **File:** `src/AgentKit/AgentRunServicesFactory.cs:45`
- **Severity:** Medium
- **Category:** validation
- **Description:** `AgentEngineBuilder.Build()` / hosted `AgentEngine`
  resolution validates catalog, run-profile reader, security selector, grant
  store, clock, ID generators, and the keyed `IAgentLoop`, but nothing in
  `AgentCompositionValidator` checks that `ISessionCoordinator`,
  `IContextAssembler`, `IToolInvoker`, `IModelCatalog`, `IModelSelector`,
  `ILlmModelResolver`, or the keyed `IRunContinuationPolicy` are registered.
  `AgentRunServicesFactory.Compile` is the first place they are resolved, and it
  runs inside `RunAgentAsync` after admission has been logged as `admitted`, a
  `RunId` minted, and a fresh security authorization captured. A composition
  that is missing any of them therefore builds successfully and fails on the
  first run with a generic `InvalidOperationException` from
  `GetRequiredService`, which is exactly the "validation deferred to the middle
  of a run" the DI invariants forbid. The builder's XML remarks acknowledge the
  graph is "explicitly partial", but the test fixture
  (`CompositionTestData.AddRunServicesFakes`) has to register all seven fakes
  purely so `RunAsync` does not throw, confirming the gap is observable.
- **Evidence:**

  ```csharp
  return new AgentRunServices(
      ResolveKeyedOrShared<ISessionCoordinator>(provider, key),
      ResolveKeyedOrShared<ISecurityProfileSelector>(provider, key),
      ResolveKeyedOrShared<IContextAssembler>(provider, key),
      ResolveKeyedOrShared<IToolInvoker>(provider, key),
      provider.GetRequiredService<IModelCatalog>(),
      ResolveKeyedOrShared<IModelSelector>(provider, key),
      ResolveKeyedOrShared<ILlmModelResolver>(provider, key),
      provider.GetRequiredKeyedService<IRunContinuationPolicy>(
          AgentLoopComponentDefaults.ContinuationPolicyKey.Value));
  ```

- **Suggested fix:** In `AgentCompositionValidator.ValidateCatalog`, for each
  runnable definition's effective loop key, check the descriptor snapshot for a
  keyed-or-unkeyed registration of every contract `AgentRunServicesFactory`
  resolves (plus the fixed continuation-policy key) and emit a diagnostic per
  missing collaborator, so `Build()` fails closed instead of the first
  `RunAsync`.
- **Confidence:** High
- **Status:** Fixed ✅

### F02 — Hook dispatcher lets observability failures fail the dispatch and skip isolated-hook rollback

- **Package:** AgentKit.Hooks
- **File:** `src/AgentKit.Hooks/DefaultHookDispatcher.cs:203`
- **Severity:** Medium
- **Category:** correctness
- **Description:** `DispatchAsync` calls
  `AgentKitDiagnostics.Activities.StartActivity` (line 97),
  `HookMetrics.RecordDispatch` (lines 123/129/140), and the `HookLog` methods
  unguarded. A throwing `ActivityListener.Sample`, a `MeterListener` measurement
  callback, or a logging provider (Microsoft's `Logger<T>` rethrows provider
  exceptions as `AggregateException` by default) therefore escapes
  `DispatchAsync` and fails the owning operation even when every hook succeeded
  — instrumentation becomes control flow, which `AGENTS.md` forbids and which
  the sibling packages (`AgentAdmissionObservability`, `IdentityObservability`,
  `DefaultTaskDelegationBroker.SafeObserve`) explicitly guard against. Worse, in
  the `Isolate` path the log/`AddEvent` calls run _before_
  `args.RestoreMutableState(snapshot)`; if the logger throws there, the isolated
  hook's partial mutation is never rolled back and the exception propagates,
  violating "isolation never leaks partial mutation". `HookMetrics` is also a
  static-initialised `Counter`, so a throwing `InstrumentPublished` callback
  turns into a `TypeInitializationException` on first dispatch.
- **Evidence:**

  ```csharp
  catch (Exception exception)
  {
      HookLog.InvocationIsolated(_logger, point, hook.Id, args.InvocationId, ...);
      _ = activity?.AddEvent(new ActivityEvent("hook.failure.isolated", ...));
      args.RestoreMutableState(snapshot);
      args.Validate();
      continue;
  }
  ```

- **Suggested fix:** Restore mutable state first in the isolation handler, then
  emit diagnostics inside a swallow-all guard; wrap `StartActivity`,
  `RecordDispatch`, and `HookLog` calls in the same failure-isolated helpers the
  other packages use (or `AgentKitActivityScope.Start`). Add
  hostile-listener/logger tests mirroring the Goals suite.
- **Confidence:** Medium
- **Status:** Fixed ✅

### F03 — Delegation broker throws `OperationCanceledException` after the child was successfully dispatched, discarding the result

- **Package:** AgentKit.Goals
- **File:** `src/AgentKit.Goals/DefaultTaskDelegationBroker.cs:110`
- **Severity:** Medium
- **Category:** async
- **Description:** After `_channel.DelegateAsync` returns, the broker calls
  `cancellationToken.ThrowIfCancellationRequested()` before classifying and
  returning the result. If the caller's token is signalled in the window between
  the channel completing and the broker returning, the child has already been
  dispatched (an external, non-reversible effect) and the grant has been
  consumed, yet the caller receives an `OperationCanceledException` instead of
  the `TaskDelegationChildResult`, losing the child identity/join handle. The
  single-use grant means a retry will be denied, so the dispatched work becomes
  untracked. This conflates caller-wait cancellation with abort, which the
  composition invariants require to be distinct. The pre-dispatch check at line
  102 is correct (nothing effected yet); the post-dispatch one is not.
- **Evidence:**

  ```csharp
  var result = await _channel.DelegateAsync(request.Prompt, cancellationToken).ConfigureAwait(false);
  cancellationToken.ThrowIfCancellationRequested();
  var outcome = result.Id == request.Prompt.Id
      ? result switch { ... }
      : TaskDelegationPublicationOutcome.Failed;
  return Complete(activity, request, outcome, started, result);
  ```

- **Suggested fix:** Remove the post-dispatch `ThrowIfCancellationRequested()`
  and always return the channel's result once `DelegateAsync` has completed; let
  the channel honour the token during dispatch.
- **Confidence:** High
- **Status:** Fixed ✅

### F04 — `SimpleAgentPlan` mints instruction messages and the local identity from ambient clock/GUIDs, so the catalog definition and conversation options disagree

- **Package:** AgentKit.Simple
- **File:** `src/AgentKit.Simple/SimpleAgentPlan.cs:177`
- **Severity:** Medium
- **Category:** correctness
- **Description:** `InstructionMessage` creates a new
  `MessageId(Guid.NewGuid())` and `DateTimeOffset.UtcNow` on every call, and
  `LocalDevelopmentIdentity()` stamps `AuthenticatedAt = DateTimeOffset.UtcNow`
  on every call. `Definition(...)` (via `SimpleAgentDefinitionSource`) and
  `Apply(ConversationSessionOptions)` each call `InstructionMessage`, so the
  `SystemMessage`s in the engine's pinned `AgentDefinition` and the ones the
  conversation session actually sends have different identities and timestamps
  for "the same" instruction; `RequireIdentity()` similarly yields a distinct
  `ExecutionIdentity` per call. This is non-deterministic framework behaviour
  that bypasses the container's `TimeProvider` and identifier generators
  (contrary to the .NET rules in `AGENTS.md`) and makes the two projections of
  the agent's instructions un-correlatable.
- **Evidence:**

  ```csharp
  private SystemMessage InstructionMessage(string text) => new(
      new MessageId(Guid.NewGuid()),
      EffectiveAgentId,
      ...
      DateTimeOffset.UtcNow,
      MessageState.Complete,
      [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
      ExtensionData.Empty);
  ```

- **Suggested fix:** Build the instruction messages and local identity once
  (memoised on the plan, or in `SimpleAgentDefinitionSource` with injected
  `TimeProvider`/`IIdentifierGenerator<MessageId>`) and reuse the same instances
  for both the definition and the conversation options.
- **Confidence:** Medium
- **Status:** Fixed ✅

### F05 — `DefaultContextAssembler` lets metric/activity failures replace the typed result

- **Package:** AgentKit.Context
- **File:** `src/AgentKit.Context/DefaultContextAssembler.cs:118`
- **Severity:** Low
- **Category:** correctness
- **Description:** `AssembleAsync` calls
  `AgentKitDiagnostics.Activities.StartActivity` (line 86) and
  `ContextMetrics.Preparations.Add` (lines 118, 135, 152) directly. A throwing
  sampler or measurement callback propagates out of the assembler as an
  exception rather than the documented `ContextAssemblyResult`, so a
  disabled/faulty observer changes the semantic outcome of context preparation.
  The same call sites in `AgentKit` and `AgentKit.Identity` are wrapped; this
  one is not, and there is no hostile-listener test in `AgentKit.Context.Tests`.
- **Evidence:**

  ```csharp
  activity.SetFailed(outcome, nameof(ContextPreparationFailureKind.EmptyHistory));
  ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
  ContextLog.Rejected(_logger, request.ModelRequestId, ContextPreparationFailureKind.EmptyHistory);
  return Task.FromResult<ContextAssemblyResult>(new ContextPreparationFailed(...));
  ```

- **Suggested fix:** Use `AgentKitActivityScope.Start` and wrap the counter/log
  calls in a swallow-all helper so observer failures cannot alter the returned
  decision.
- **Confidence:** Medium
- **Status:** Fixed ✅

### F06 — `Build()` failure path disposes the provider synchronously, which throws and masks the real error for `IAsyncDisposable`-only services

- **Package:** AgentKit
- **File:** `src/AgentKit/AgentEngineBuilder.cs:106`
- **Severity:** Low
- **Category:** resource-leak
- **Description:** `AgentCompositionValidator.Validate(provider)` activates
  singletons (catalog, profile reader, `ISecurityProfileSelector`,
  `TimeProvider`, ID generators) before validation can fail. If any of those
  application-supplied singletons implements only `IAsyncDisposable`, Microsoft
  DI's `ServiceProvider.Dispose()` throws `InvalidOperationException` ("... only
  implements IAsyncDisposable"), which replaces the `AgentCompositionException`
  in the `catch` block and leaves the remaining services undisposed. The remarks
  promise "a failed build leaks nothing".
- **Evidence:**

  ```csharp
  catch
  {
      provider?.Dispose();
      throw;
  }
  ```

- **Suggested fix:** Wrap the cleanup in its own `try/catch` and attach any
  disposal failure as inner/aggregate data so the original composition error
  survives; consider offering `BuildAsync` (or disposing via `DisposeAsync` when
  no synchronous context is required) so async-only singletons are released.
- **Confidence:** Medium
- **Status:** Fixed ✅

### F07 — `Agent.RunAsync` documents `ObjectDisposedException` but the engine performs security capture before any disposed check, and hosted engines never throw

- **Package:** AgentKit
- **File:** `src/AgentKit/AgentEngine.cs:272`
- **Severity:** Low
- **Category:** contract-mismatch
- **Description:** `Agent.RunAsync` (`src/AgentKit/Agent.cs:93`) declares
  `ObjectDisposedException` when the owning engine is disposed, but
  `AgentEngine` has no disposed flag. For a standalone engine the only thing
  that eventually throws is `Services.CreateAsyncScope()` at line 289 — after
  the catalog read, `_runIds.Create()`, the run-profile read, and a fresh
  `ISecurityProfileSelector.SelectAsync` authorization capture (a
  security-relevant, possibly audited effect) have all run against a disposed
  container. For a host-managed engine `DisposeAsync` is a no-op, so runs
  continue to be admitted after disposal and the documented exception never
  occurs.
- **Evidence:**

  ```csharp
  var runId = _runIds.Create();
  var runCorrelation = new InRunOperationCorrelation(_operationIds.Create(), runId, turnId: null);
  var security = pinnedPublication.SecurityProfile;
  var authorizationResult = await _securityProfiles.SelectAsync(..., cancellationToken).ConfigureAwait(false);
  ...
  await using var scope = Services.CreateAsyncScope();
  ```

- **Suggested fix:** Track disposal in `AgentEngine` (set under `_disposeLock`)
  and call `ObjectDisposedException.ThrowIf(...)` at the top of `RunAgentAsync`,
  `GetAgentAsync`, and `GetAgentsAsync`, so no identity is minted or
  authorization captured after disposal on either ownership path.
- **Confidence:** High
- **Status:** Fixed ✅

### F08 — Resolving `AgentEngine` from a standalone engine's `Services` yields a second, unowned engine

- **Package:** AgentKit
- **File:** `src/AgentKit/ServiceExtensions.cs:60`
- **Severity:** Low
- **Category:** contract-mismatch
- **Description:** `AgentEngineBuilder`'s constructor calls `AddAgentKit()`,
  which registers a hosted-style `AgentEngine` singleton factory. `Build()`
  never resolves it (it constructs the owning engine directly), but the
  registration remains in the provider that `AgentEngine.Services` exposes.
  `engine.Services.GetRequiredService<AgentEngine>()` therefore runs
  `AgentCompositionValidator.Validate` a second time and returns a _different_
  `AgentEngine` with `ownedProvider: null`, so two engine instances with
  different disposal semantics exist over one container and `Agent` handles
  obtained from each are not interchangeable by reference. Nothing in the tests
  exercises this path.
- **Evidence:**

  ```csharp
  services.TryAddSingleton(
      static provider =>
      {
          var composition = AgentCompositionValidator.Validate(provider);
          return new AgentEngine(provider, ownedProvider: null, composition);
      });
  ```

- **Suggested fix:** In `Build()`, replace the `AgentEngine` descriptor with an
  instance registration bound to the engine being constructed (or a factory that
  returns it), so the container resolves the same owning engine.
- **Confidence:** Medium
- **Status:** Fixed ✅

### F09 — Instructions bypass state and tool-part validation in `DefaultContextAssembler`

- **Package:** AgentKit.Context
- **File:** `src/AgentKit.Context/DefaultContextAssembler.cs:140`
- **Severity:** Low
- **Category:** validation
- **Description:** `Instructions` is `ImmutableArray<AgentMessage>` (not
  restricted to system/developer roles), and the assembler prepends it verbatim:
  `RepairHistory`, `ValidateRolePartCombinations`, and
  `ValidateToolCallCausality` only run over `history`. An instruction message
  that is not `MessageState.Complete`, or that carries a
  `ToolCallPart`/`ToolResultPart` (or is a `ToolMessage`/`UserMessage` placed in
  the instruction slot), is sent to the provider unrepaired and unvalidated,
  defeating the causality and completeness guarantees the class documents for
  the request it produces.
- **Evidence:**

  ```csharp
  var structuralFailure = ValidateRolePartCombinations(repairedHistory) ?? ValidateToolCallCausality(repairedHistory);
  ...
  var messages = instructions.AddRange(repairedHistory);
  ```

- **Suggested fix:** Validate `instructions` as well: require `Complete` state
  and system/developer role (or at minimum run the role/part validators over the
  combined `messages`), returning a typed `ContextPreparationFailed` on
  violation.
- **Confidence:** Medium
- **Status:** Fixed ✅

### F10 — `AddAgentIdentity` options validation is not run at startup; invalid values surface as `ArgumentOutOfRangeException` on first identity use

- **Package:** AgentKit.Identity
- **File:** `src/AgentKit.Identity/ServiceExtensions.cs:24`
- **Severity:** Low
- **Category:** validation
- **Description:** Unlike `AddAgentHooks` and `AddAgentArtifacts`,
  `AddAgentIdentity` registers `.Validate(...)` rules without
  `.ValidateOnStart()`. Neither Microsoft DI `ValidateOnBuild` nor
  `AgentCompositionValidator` runs options validators, so a misconfigured
  `MaximumDelegationDepth`/`MaximumClockSkew`/`MaximumEvidenceLifetime` is first
  detected when `AgentIdentityOptionsSnapshot` is materialised inside the first
  `ResolveAsync`/`DeriveAsync`, and it fails with the snapshot constructor's
  `ArgumentOutOfRangeException` rather than an `OptionsValidationException` at
  the composition boundary.
- **Evidence:**

  ```csharp
  var options = services.AddOptions<AgentIdentityOptions>()
      .Validate(static value => value.MaximumDelegationDepth > 0, "MaximumDelegationDepth must be positive.")
      .Validate(static value => value.MaximumClockSkew >= TimeSpan.Zero, "MaximumClockSkew must not be negative.")
      .Validate(static value => value.MaximumEvidenceLifetime > TimeSpan.Zero, "MaximumEvidenceLifetime must be positive.");
  ```

- **Suggested fix:** Append `.ValidateOnStart()` (and consider having the
  composition validator resolve `IOptions<AgentIdentityOptions>` when the
  identity package is present).
- **Confidence:** High
- **Status:** Fixed ✅

### F11 — `UseSqliteSessions` performs a file-system mutation during registration

- **Package:** AgentKit.Simple
- **File:** `src/AgentKit.Simple/AgentEngineBuilderExtensions.cs:123`
- **Severity:** Low
- **Category:** correctness
- **Description:** The registration extension calls `Directory.CreateDirectory`
  immediately, before `Build()`. Registration extensions are supposed to only
  modify the service collection; here merely configuring the builder (including
  in a code path that later fails validation or never builds) creates
  directories on disk, and any I/O failure surfaces as an unrelated exception
  from a configuration call. The `SqliteSessionStoreTarget` is already
  configured with `CreateIfMissing`, so the eager creation is redundant with the
  store's own open-time behaviour.
- **Evidence:**

  ```csharp
  var fullPath = Path.GetFullPath(databasePath);
  _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
  var target = new SqliteSessionStoreTarget(fullPath, instanceId ?? DefaultSqliteInstanceId,
      SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
  ```

- **Suggested fix:** Drop the eager `CreateDirectory` and let the SQLite store's
  `CreateIfMissing` open mode create the parent, or defer it to the store's
  first open.
- **Confidence:** Medium
- **Status:** Fixed ✅

---

### Areas reviewed (Group 09)

- `AgentEngine`, `AgentEngineBuilder`, `AgentKitServiceProviderFactory`,
  `AgentCompositionValidator`, `ComponentRegistrationSnapshot`,
  `AgentRunServicesFactory`, `Agent`, `DefaultAgentDefinitionCatalog`,
  `DefaultAgentRunProfilePublicationReader`, `ServiceExtensions`,
  `AgentAdmissionObservability`, `AgentCompositionBuildObservability`,
  `NonBlockingInstrument` (full reads); `ComponentDependencyGraphValidator`
  SCC/cycle logic (partial read).
- `AgentKit.Simple`: `AgentEngineBuilderExtensions`, `SimpleAgentPlan`,
  `SimpleAgentDefinitionSource`, `AgentEngineExtensions`.
- `AgentKit.Hooks`: `HookOrdering`, `DefaultHookDispatcher`, `AgentHookOptions`,
  `HookMetrics`, `ServiceExtensions`, plus
  `AgentHookEventArgs`/`HookDispatchScope`/`HookPriority` contracts.
- `AgentKit.Identity`: `ExecutionIdentityResolver`,
  `DefaultDelegatedIdentityDeriver`, `DefaultIdentityValidationPolicy`,
  `IdentityIssuerCatalog`, `OrderedIdentityNormalizationPolicies`,
  `IdentityObservability`, `IdentityMetrics`, `ServiceExtensions`, plus
  `ExecutionIdentity`/`DelegationIdentityLink`/`AuthenticationEvidence`
  contracts.
- `AgentKit.Observability`: `AgentKitDiagnostics`, `AgentKitActivityScope`,
  `ActivityExtensions`, `ServiceExtensions`; tag-name scan for content-bearing
  names.
- `AgentKit.Context`: `DefaultContextAssembler`, `ContextLog`, `ContextMetrics`,
  `ServiceExtensions`.
- `AgentKit.Goals`: `DefaultTaskDelegationBroker`,
  `TaskDelegationEnforcementReceipt`, `GoalsMetrics`, `ServiceExtensions`.
- `AgentKit.Artifacts`: `DefaultArtifactCoordinator`,
  `ArtifactProcessOutputSink`, `ServiceExtensions`;
  `AgentKit.Artifacts.InMemory`: `InMemoryArtifactStore`,
  `ArtifactEnforcementReceipt`, `ReplayKey`, `ServiceExtensions`.
- Mirror tests consulted: `AgentKit.Tests` (builder/engine/composition
  fixtures), `AgentKit.Simple.Tests` (ordering of
  `UseSqliteSessions`/`UseLocalDevelopmentDefaults`), `AgentKit.Hooks.Tests`
  (test inventory), `AgentKit.Goals.Tests` (cancellation, id-mismatch, hostile
  sampler cases).
- Repository-wide greps for `UtcNow`, `Guid.NewGuid`, `ThreadStatic`,
  `AsyncLocal`, `BuildServiceProvider`, string interpolation in logs, `lock`,
  `TryAdd`.

### Areas not reviewed (Group 09)

- `ComponentInfrastructureGraphMaterializer` and
  `ComponentRegistrationCorrespondenceValidator` internals beyond a structural
  skim (heavily covered by dedicated tests).
- `AgentKitActivityNames` / `AgentKitMetricNames` naming completeness.
- Concrete hook point definitions and their `EventArgs` writable surfaces (live
  in other contract assemblies, outside this group).
- `AgentKit.Conversations`' `DefaultConversationSession` (composed by
  `AgentKit.Simple` but owned elsewhere); noted only that it drives the loop
  directly rather than through `AgentEngine` admission.
- `AgentKit.Compatibility.Tests` API snapshots were not diffed.
- Identity issuer implementations and claims parsing (no first-party issuer
  exists in this scope).

## Group 10 — Context.Compaction, Durability.InMemory, Mcp, Mcp.Client, Mcp.Server

Read-only review of the compaction pipeline (cut selector, extractive and model
strategies, validator, compactor/activation, prompt resources), the in-memory
durability adapter (lease manager, fencing, journal, evidence load), the shared
MCP reflection contracts, the MCP client (factory, activator, typed client, SDK
caller, version policy) and the small MCP server surface. The compaction and
durability code is generally careful: cut safety is derived from
`CausalParentId` and independently re-derived by the validator, activation is
version-checked against `SourceVersion`, fencing tokens are allocated under one
gate, expiry uses the injected `TimeProvider`, and cancellation is not swallowed
anywhere. No ambient clocks, `Guid.NewGuid` outside the documented replaceable
generators, or `catch { }` swallowing cancellation were found. Findings: **0
High, 3 Medium, 3 Low** (6 total). The most consequential are a
reflection-identity mismatch that makes `McpToolContract<TTools>.Resolve` reject
inherited (non-overridden) tool methods at call time, an MCP client version
policy whose `RequireAtLeast` actually pins negotiation to exactly that
revision, and a compaction source ceiling computed over the entire branch prefix
so compaction becomes permanently impossible once a branch exceeds
`MaximumSourceEntries`.

---

### M01 — `McpToolContract<TTools>.Resolve` rejects inherited (non-overridden) attributed methods because `MethodInfo` identity includes `ReflectedType`

- **Package:** AgentKit.Mcp
- **File:** `src/AgentKit.Mcp/McpToolContract.cs:44` (index build) and
  `src/AgentKit.Mcp/McpToolContract.cs:59` (lookup)
- **Severity:** Medium
- **Category:** correctness
- **Description:** The contract indexes descriptors by the `MethodInfo` returned
  from `typeof(TTools).GetMethods(...)`, whose `ReflectedType` is `TTools`.
  `McpToolClient<TTools>.CallAsync` resolves `MethodCallExpression.Method`,
  which the C# compiler materializes via `GetMethodFromHandle` with
  `ReflectedType == DeclaringType`. `RuntimeMethodInfo.Equals` is reference
  equality for non-generic methods and the runtime caches one instance per
  (handle, reflectedType), so when `TTools` inherits an `[McpTool]` method from
  a base class without overriding it (a shape the contract explicitly supports
  through `GetCustomAttribute(inherit: true)` and
  `McpToolAttribute(Inherited = true)`), the dictionary lookup misses and every
  call fails with
  `ArgumentException("Method '...' is not an MCP tool in '...'")`. The
  refresh/catalog path succeeds, so the failure only surfaces at first
  invocation. Verified empirically with a minimal repro
  (`getMethods.Reflected=Derived expr.Reflected=Base equal=False dictHit=False`);
  the existing test only covers the override case, where the derived
  `MethodInfo` is the one bound by the expression.
- **Evidence:**

```csharp
_methodsByReflectionIdentity = duplicate is null
    ? methods.ToImmutableDictionary(static descriptor => descriptor.Method)
    : throw ...;
...
public McpToolMethodDescriptor Resolve(MethodInfo method)
{
    ArgumentNullException.ThrowIfNull(method);
    return _methodsByReflectionIdentity.TryGetValue(method, out var descriptor)
        ? descriptor
        : throw new ArgumentException($"Method '{method.Name}' is not an MCP tool in '{typeof(TTools)}'.", nameof(method));
```

- **Suggested fix:** Key the index by `MethodInfo.MethodHandle` (plus declaring
  type handle for generic owners) or normalize both sides with
  `method.GetBaseDefinition()`/`MethodBase.GetMethodFromHandle(method.MethodHandle)`
  before comparison; add a test for a derived contract that inherits without
  overriding.
- **Confidence:** High
- **Status:** Fixed ✅

### M02 — `McpClientVersionPolicy.RequireAtLeast` pins negotiation to exactly the "minimum" revision (never a newer one)

- **Package:** AgentKit.Mcp.Client
- **File:** `src/AgentKit.Mcp.Client/McpToolClientFactory.cs:56` (and
  `src/AgentKit.Mcp.Client/McpClientVersionPolicy.cs:22`)
- **Severity:** Medium
- **Category:** contract-mismatch
- **Description:** The factory maps `MinimumVersion` straight onto
  `McpClientOptions.ProtocolVersion`. In ModelContextProtocol 2.2.0 that option
  is documented as "both the requested version and the minimum the client will
  accept: the client requests exactly this version ... and fails if the server
  negotiates a different version." Per the MCP spec a server that supports the
  requested revision answers with that revision, so `RequireAtLeast(2025-03-26)`
  against a 2025-11-25-capable server negotiates 2025-03-26 (a _downgrade_
  relative to `Automatic`), and a server that answers with a newer revision it
  prefers causes an `McpException`. The public name, the `MinimumVersion`
  property, and the XML remarks ("prevents silent downgrade below that
  revision", "minimum acceptable protocol revision") promise floor semantics
  that the implementation does not provide. Tests only exercise the case where
  the server is also pinned to the same exact revision.
- **Evidence:**

```csharp
var policy = versionPolicy ?? McpClientVersionPolicy.Automatic;
var options = new McpClientOptions { ProtocolVersion = policy.MinimumVersion?.ToString() };
```

```csharp
/// <summary>Creates a policy that refuses a negotiated revision below <paramref name="version"/>.</summary>
public static McpClientVersionPolicy RequireAtLeast(McpProtocolVersion version)
```

- **Suggested fix:** Either rename to `RequireExactly` and fix the docs, or
  implement a real floor: connect with `ProtocolVersion = null` (automatic),
  then compare `client.NegotiatedProtocolVersion` against `MinimumVersion` and
  dispose/throw a typed exception when it is lower.
- **Confidence:** High (behavior); Medium (whether maintainers intend exact-pin
  semantics)
- **Status:** Fixed ✅

### M03 — Compaction source ceiling and transcript are computed over the whole branch prefix, so compaction becomes permanently impossible past `MaximumSourceEntries`

- **Package:** AgentKit.Context.Compaction
- **File:**
  `src/AgentKit.Context.Compaction/StructuralCompactionCutSelector.cs:57`
  (ceiling) and `src/AgentKit.Context.Compaction/DefaultCompactor.cs:347` (read
  from sequence 0)
- **Severity:** Medium
- **Category:** correctness
- **Description:** `LoadSourceAsync` always reads the branch from
  `SessionSequence(0)` and hands the selector every entry `<= SourceThrough`,
  including entries already covered by earlier active `CompactionSessionEntry`
  records. The selector rejects with `SourceLimitExceeded` when that count
  exceeds `MaximumSourceEntries` (default 5,000). Because a compaction never
  shrinks the branch (records are append-only), once a long-lived branch
  accumulates more than the ceiling the compactor rejects every subsequent
  request deterministically, regardless of how much has already been
  checkpointed — the feature silently stops working for exactly the sessions
  that need it most. The same design makes each model-backed attempt re-render
  all raw covered history (not just the newest checkpoint plus the delta) into
  the transcript, where head/tail truncation can drop the earlier summary
  sitting in the middle.
- **Evidence:**

```csharp
var entries = request.Source.Entries;

if (entries.Length > _maximumSourceEntries)
{
    return ValueTask.FromResult<CompactionCutSelectionResult>(new NoSafeCompactionCut(
        new CompactionRejection(
            CompactionRejectionKind.SourceLimitExceeded,
```

```csharp
var entries = ImmutableArray.CreateBuilder<SessionEntry>();
var cursor = new SessionSequence(0);
```

- **Suggested fix:** Start the eligible source at the newest active record's
  `RetainedSuffixStart` (treating that record as the first covered entry) or, at
  minimum, apply `MaximumSourceEntries` only to entries after the newest active
  checkpoint; add a test that compacts twice on a branch longer than the
  ceiling.
- **Confidence:** Medium
- **Status:** Fixed ✅

### M04 — `DefaultCompactor` never populates `CompactionRecord.Supersedes` even when the cut covers an earlier active record

- **Package:** AgentKit.Context.Compaction
- **File:** `src/AgentKit.Context.Compaction/DefaultCompactor.cs:484`
- **Severity:** Low
- **Category:** contract-mismatch
- **Description:** `docs/architecture/context-compaction.md` states "A newer
  active record names the prior `CompactionId` in `Supersedes`; it does not
  mutate the older record." The compactor hard-codes `supersedes: null` although
  the covered entries are available at activation and the model strategy already
  recognizes `CompactionSessionEntry` in the covered set (rendering it as
  `[earlier summary]`). Consumers that walk the supersession chain (audit,
  reconciliation, export) see every record as a root, and the durable lineage
  the architecture requires is lost.
- **Evidence:**

```csharp
var record = new CompactionRecord(
    context,
    request.SourceVersion,
    activatedVersion,
    CompactionRecordStatus.Active,
    manifest,
    candidate.Checkpoint,
    supersedes: null,
    rejection: null,
```

- **Suggested fix:** Scan `coveredEntries` for the newest
  `CompactionSessionEntry { Record.Status: Active }` and pass its
  `Context.CompactionId` as `supersedes`; add a validator check that
  `Supersedes` is set when such an entry is covered.
- **Confidence:** Medium
- **Status:** Fixed ✅

### M05 — MCP client leaks the connected SDK session if `SdkMcpToolCaller` construction throws after `McpClient.CreateAsync` succeeds

- **Package:** AgentKit.Mcp.Client
- **File:** `src/AgentKit.Mcp.Client/McpToolClientFactory.cs:73`
- **Severity:** Low
- **Category:** resource-leak
- **Description:** `ConnectAsync` documents that transport ownership transfers
  to the returned client, and the activator disposes the caller when catalog
  validation fails. However `new SdkMcpToolCaller(sdkClient)` is evaluated as an
  argument before `McpToolClientActivator.CreateAsync` runs; its constructor
  throws `InvalidOperationException` when `NegotiatedProtocolVersion` is null
  and `FormatException` when it is not canonical `yyyy-MM-dd`. On that path the
  already-connected `McpClient` (and for stdio transports, its child process) is
  never disposed — the `catch` blocks only log and rethrow. The same gap exists
  inside `McpToolClientActivator.CreateAsync` for anything thrown before its
  `try` (contract construction, serializer copy, logger creation) while it
  already owns `caller`.
- **Evidence:**

```csharp
var sdkClient = await McpClient.CreateAsync(transport, options, effectiveLoggerFactory, cancellationToken).ConfigureAwait(false);

var client = await McpToolClientActivator.CreateAsync(
    new SdkMcpToolCaller(sdkClient),
    _contract,
    serializerOptions,
    effectiveLoggerFactory,
    cancellationToken).ConfigureAwait(false);
```

- **Suggested fix:** Wrap caller construction and activation in a `try` that
  awaits `sdkClient.DisposeAsync()` on any exception; in the activator, move
  `caller` ownership into the `try` (or use `await using` with an ownership flag
  cleared on success).
- **Confidence:** Medium (path requires an SDK that reports a null/non-canonical
  negotiated version, which 2.2.0 validates against its own list)
- **Status:** Fixed ✅

### M06 — Journal writes mutate state before the fallible `GetUtcNow()` call, so a clock failure reports an exception for a committed write

- **Package:** AgentKit.Durability.InMemory
- **File:**
  `src/AgentKit.Durability.InMemory/InMemoryDurableOperationJournal.cs:79` (also
  `:75`, `:110-114`, `:146-150`)
- **Severity:** Low
- **Category:** correctness
- **Description:** In all three write methods the record is inserted/mutated
  (`_records[address] = ...`, `existing.State = ...`,
  `existing.TerminalResult = ...`) and only then is `_timeProvider.GetUtcNow()`
  called to build the `DurableRecorded` result. If the injected clock throws (a
  scenario the package's own tests treat as real —
  `RecordStartAsync_WhenTheClockFailsDuringTheWriteAndLoggingIsEnabled_RecordsFailureEventAndPropagates`),
  the caller receives an exception while the journal has already committed the
  transition; the contract's `DurableRecordFailed(committed: …)` channel is
  bypassed and the caller cannot tell the write landed. The lease manager avoids
  this by reading `now` before mutating.
- **Evidence:**

```csharp
_records[address] = new DurableOperationRecord(binding, start.FencingToken);
return Recorded(start.FencingToken, _timeProvider.GetUtcNow());
```

```csharp
existing.State = result.State;
existing.SideEffectCertainty = result.SideEffectCertainty;
existing.TerminalResult = result;
existing.LastWriterToken = result.FencingToken;
return Recorded(result.FencingToken, _timeProvider.GetUtcNow());
```

- **Suggested fix:** Capture `var recordedAt = _timeProvider.GetUtcNow();`
  before taking the gate (as `AcquireAsync` does) and use it for the result, so
  no state mutates if the clock fails.
- **Confidence:** Medium
- **Status:** Fixed ✅

---

### Areas reviewed (Group 10)

- `AgentKit.Context.Compaction`: `StructuralCompactionCutSelector`
  (causal-parent safety, user-turn preference, retention minimum),
  `DefaultCompactor` (pinned paged load, version conflict, ineligible-tail
  parent check, activation, cancellation/conflict/failure reconciliation,
  idempotent record discovery), `ExtractiveCompactionStrategy`,
  `ModelCompactionStrategy` (model selection, single request, system/developer
  omission, stop-reason handling, truncation, provenance),
  `DefaultCompactionValidator` (emptiness, ceiling, prefix/range/suffix
  coherence, manifest coherence, broken causality, reduction ratio),
  `CharacterCompactionSizeEstimator`, `CompactionTextTruncation` (surrogate
  handling), `ContentTextExtractor`, `CompactionPromptResources` (embedded
  resource, culture-neutral, bounded), `CompactionOptions` and
  `ServiceExtensions`, and cross-checked `DefaultAgentLoop` tool-entry causal
  linking and `CompactionCheckpointProjector` (runtime role, not system).
- `AgentKit.Durability.InMemory`: `InMemoryDurableLeaseManager` (atomic
  acquire/takeover under one gate, monotonic tokens, TimeProvider expiry,
  renewal of expired lease refused, release only for current generation),
  `InMemoryExecutionLease` (single dispose), `InMemoryDurableOperationJournal`
  (fencing on every write, binding check, terminal idempotency, evidence
  derivation), `ServiceExtensions`, and the mirror tests.
- `AgentKit.Mcp`: `McpToolContract`, `McpToolMethodDescriptor`,
  `McpToolAttribute`, `McpToolName`, `McpProtocolVersion(s)`,
  `McpCatalogVersion`, `ServiceExtensions`.
- `AgentKit.Mcp.Client`: `McpToolClientFactory`, `McpToolClientActivator`,
  `McpToolClient` (refresh gate, immutable snapshot publication, dispose-once,
  cancellation propagation), `SdkMcpToolCaller` (IsError mapping,
  structured-content requirement, untrusted `_meta` version read),
  `McpClientVersionPolicy`, logs/metrics tag cardinality, and the SDK 2.2.0
  option semantics via its shipped XML docs.
- `AgentKit.Mcp.Server`: `McpServerBuilderExtensions`, `McpServerVersionPolicy`,
  `ServiceExtensions`.
- Repository-wide greps for ambient clocks, `Guid.NewGuid`, blocking waits,
  culture-sensitive parsing, and blanket `catch` in the scoped packages.

### Areas not reviewed (Group 10)

- Internal behavior of the `ModelContextProtocol` 2.2.0 SDK itself (stdio
  process lifetime/stderr draining, HTTP redirect/auth header handling, OAuth
  callback binding, `tools/list_changed` notifications, request timeouts) —
  AgentKit does not implement transports; only the SDK's public option contracts
  were consulted.
- `AgentKit.Loop`, `AgentKit.Context`, and `AgentKit.Session` consumers of
  compaction records beyond the two files spot-checked for causal linking and
  projection role.
- Conformance suites under `tests/AgentKit.Conformance/` (none target durability
  or MCP contracts).
- Observability wiring correctness beyond tag cardinality and content-freedom
  (activity parentage, event-ID uniqueness across packages).
- Build/`obj` artifacts and generated `AssemblyInfo.cs` files.
