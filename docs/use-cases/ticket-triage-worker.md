# Background ticket-triage worker

A help desk receives a few thousand tickets a day. A worker service pulls each
new ticket from a queue, has an agent classify it, draft a first response, and
propose a priority, then writes the result back to the ticketing system. Nobody
is watching; the worker has to bound every job's cost and duration, stop cleanly
when the host shuts down, emit traces and metrics into the platform's telemetry,
and keep a required audit trail because the agent acts on behalf of the company,
not a person.

## What the agent needs

| Need                            | AgentKit part                                                                                       |
| ------------------------------- | --------------------------------------------------------------------------------------------------- |
| Run inside a .NET worker        | `AgentEngine.CreateBuilder()` in a `BackgroundService`; one engine per job                          |
| A service identity              | `WithIdentity` with `ExecutionSubjectKind.Service`                                                  |
| Bounded jobs                    | `WithMaxTurns`, `WithAttemptTimeout`, a per-job `CancellationTokenSource` linked to `stoppingToken` |
| Write back to the ticket system | A custom `ITool` (see [Order lookup with your own tool](domain-tool-integration.md))                |
| Traces and metrics              | OpenTelemetry `AddSource("AgentKit")` and `AddMeter("AgentKit")`                                    |
| Durable, required audit         | `AddSecurityAuditSink` with `SecurityAuditDelivery.Required`, SQLite grant and session stores       |

## Compose the host

Telemetry and the shared stores belong to the host. The engine is built once and
registered as a singleton; each ticket becomes one session on it, so jobs are
isolated by session while sharing the model catalog, stores, and audit sink:

```csharp
var host = Host.CreateApplicationBuilder(args);

host.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(AgentKitDiagnostics.ActivitySourceName).AddOtlpExporter())
    .WithMetrics(m => m.AddMeter(AgentKitDiagnostics.MeterName).AddOtlpExporter());

host.Services.AddSingleton<ITicketQueue, ServiceBusTicketQueue>();
host.Services.AddSingleton<ITicketApi, HttpTicketApi>();
host.Services.AddSingleton<ISecurityAuditSink, AuditLogSink>();
host.Services.AddSingleton(sp => TriageWorker.CreateEngine(
    host.Configuration["OpenAI:ApiKey"]!, sp.GetRequiredService<ISecurityAuditSink>()));
host.Services.AddHostedService<TriageWorker>();

await host.Build().RunAsync();
```

## Compose the engine

```csharp
sealed class TriageWorker(ITicketQueue queue, ITicketApi tickets, AgentEngine engine, ILogger<TriageWorker> logger)
    : BackgroundService
{
    static readonly ExecutionIdentity WorkerIdentity = ExecutionIdentity.ForService(
        new TenantId("acme"),
        new PrincipalId("triage-worker"),
        new IdentityIssuerId("azure-managed-identity"),
        "managed-identity",
        authenticatedAt: DateTimeOffset.UtcNow);

    internal static AgentEngine CreateEngine(string apiKey, ISecurityAuditSink audit)
    {
        var builder = AgentEngine.CreateBuilder()
            .UseOpenAI(apiKey, "gpt-4o-mini")
            .UseSqliteSessions("/var/lib/triage/sessions.db")
            .WithIdentity(WorkerIdentity)
            .WithInstructions("You triage support tickets: classify, set a priority from 1 (urgent) to 4, and draft a first reply.")
            .WithOutput<TriageDecision>("""
                {
                  "type": "object",
                  "properties": {
                    "category": { "type": "string", "enum": ["billing", "bug", "how-to", "account", "other"] },
                    "priority": { "type": "integer", "minimum": 1, "maximum": 4 },
                    "draftReply": { "type": "string" }
                  },
                  "required": ["category", "priority", "draftReply"],
                  "additionalProperties": false
                }
                """)
            .WithRequestSettings(LlmRequestSettings.Default with { Temperature = 0.2, MaxOutputTokens = 1_500 })
            .WithMaxTurns(4)
            .WithAttemptTimeout(TimeSpan.FromSeconds(60))
            .WithBudget(o =>
            {
                o.MaxModelRequests = 4;
                o.MaxInputTokens = 20_000;
                o.MaxCostUsd = 0.02m;
            });

        builder.Services.AddSqliteSecurityGrantStore(new SqliteSecurityGrantStoreTarget(
            "/var/lib/triage/grants.db", GrantStoreInstanceId,
            SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations));
        builder.Services.AddSecurityAuditSink(
            new SecurityAuditSinkRegistration(AllAuditEventKinds, SecurityAuditDelivery.Required, providesDurableAcceptance: true),
            audit);
        builder.Services.AddSingleton<ISecurityPolicy, TriagePolicy>();

        return builder.Build();
    }

    sealed record TriageDecision(string Category, int Priority, string DraftReply);
}
```

`TriagePolicy` allows session state (`StateRead`, `StateMutation`) and abstains
otherwise, so any tool that would need a file, process, or network is denied by
default. The agent has no tools at all: it reads the ticket in the prompt and
returns a `TriageDecision`; the worker, not the model, writes to the ticket
system. `WithOutput<T>` validates each final answer against the schema and asks
the model to repair an invalid one before the turn fails.

## Use it

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var triage = (await engine.GetAgentAsync((await engine.GetAgentsAsync(stoppingToken)).Single().Id, stoppingToken))!;

    await Parallel.ForEachAsync(queue.ReadAllAsync(stoppingToken), new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = stoppingToken }, async (ticket, ct) =>
    {
        using var jobCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        jobCancellation.CancelAfter(TimeSpan.FromMinutes(3));

        var result = await triage.SendAsync(new AgentSendRequest(WorkerIdentity, TicketPrompt(ticket)), jobCancellation.Token);

        var usage = result.NewMessages.OfType<AssistantMessage>().Select(m => m.Response.Usage)
            .Where(u => u.ReportState != ModelUsageReportState.NotReported).ToList();
        logger.LogTriageCompleted(ticket.Id, result.SessionId, result.Outcome.GetType().Name,
            usage.Sum(u => u.InputTokens ?? 0), usage.Sum(u => u.OutputTokens ?? 0), usage.Sum(u => u.EstimatedCost ?? 0m));

        if (result.Outcome is AgentRunCompleted { Output.Value: TriageDecision decision })
        {
            await tickets.UpdateAsync(ticket.Id, decision.Category, decision.Priority, decision.DraftReply, ct);
        }
        else
        {
            await queue.DeadLetterAsync(ticket, result.Outcome.GetType().Name, ct);
        }
    });
}
```

Eight tickets run at once on the one engine, each in its own session; the
engine's per-session lane never contends because every ticket is a new session.

`stoppingToken` flows into every turn, so host shutdown cancels the model call
and any in-flight tool, the run settles as `AgentRunCancelled`, and the ticket
is dead-lettered instead of updated from a half-formed answer. The per-job
`CancelAfter` is an outer bound above the attempt timeout; both produce a typed
outcome rather than an exception from the engine.

## What the framework guarantees

- **Telemetry is observational.** Every operation with duration creates an
  `Activity` under the `AgentKit` source and bounded aggregates go to the
  `AgentKit` meter, with typed identities as tags. Prompts, model output, tool
  arguments, and results are never emitted by default. With no listener
  attached, behavior is unchanged.
- **Audit is a precondition, not a side effect.** With delivery `Required`, an
  audited operation whose record cannot be accepted by the sink does not run.
- **Cancellation is cooperative at every await.** Provider streaming, tool
  invocation, and session appends all honor the token; the session store is
  never left mid-append.
- **Usage is exact or unknown.** `ModelUsage` numbers are `null` when the
  provider did not report them; the cost sum above under-reports rather than
  inventing a value, and the budget never charges an unreported dimension.
- **A cap is a typed stop.** When a reservation is refused the run settles as
  `AgentRunBudgetExhausted` naming the dimension; the worker sees a failed
  outcome and dead-letters the ticket instead of an exception.

## Status

One engine, many concurrent sessions, typed output, and per-run budgets are all
in place. `WithBudget` counts turns, model requests, and tool calls before each
attempt and accounts reported tokens and cost after each response, so a single
response may cross a token or cost cap before the run stops; pre-effect
estimation of unknown token cost is tracked in the
[implementation ledger](../implementation-progress.md#component-coverage). The
in-memory ledger accounts within this process; register `AddSqliteBudgetLedger`
before `WithBudget` for durable accounting.

## What lives where

| Concern                         | Package                                                                                                                                                |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Activity source and meter names | [AgentKit.Observability](../../src/AgentKit.Observability/README.md)                                                                                   |
| Audit sinks and policies        | [AgentKit.Permissions](../../src/AgentKit.Permissions/README.md)                                                                                       |
| SQLite grants and sessions      | [AgentKit.Permissions.Sqlite](../../src/AgentKit.Permissions.Sqlite/README.md), [AgentKit.Session.Sqlite](../../src/AgentKit.Session.Sqlite/README.md) |
| Tool allow-list                 | [AgentKit.Tools](../../src/AgentKit.Tools/README.md)                                                                                                   |
| Budget contracts                | [AgentKit.Budgets](../../src/AgentKit.Budgets/README.md)                                                                                               |
| Normative observability rules   | [Observability and audit](../concepts/observability-and-audit.md)                                                                                      |

Next: [Order lookup with your own tool](domain-tool-integration.md) ·
[Composing an application](../guides/composition.md)
