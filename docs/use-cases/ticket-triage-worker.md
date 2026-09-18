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

Telemetry and the shared stores belong to the host; the agent engine is built
per job so that each job's session, limits, and cancellation are isolated:

```csharp
var host = Host.CreateApplicationBuilder(args);

host.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(AgentKitDiagnostics.ActivitySourceName).AddOtlpExporter())
    .WithMetrics(m => m.AddMeter(AgentKitDiagnostics.MeterName).AddOtlpExporter());

host.Services.AddSingleton<ITicketQueue, ServiceBusTicketQueue>();
host.Services.AddSingleton<ITicketApi, HttpTicketApi>();
host.Services.AddSingleton<ISecurityAuditSink, AuditLogSink>();
host.Services.AddHostedService<TriageWorker>();

await host.Build().RunAsync();
```

## Compose the engine

```csharp
sealed class TriageWorker(ITicketQueue queue, ITicketApi tickets, ISecurityAuditSink audit, IConfiguration config, ILogger<TriageWorker> logger)
    : BackgroundService
{
    static readonly ExecutionIdentity WorkerIdentity = new(
        new TenantId("acme"),
        new PrincipalId("triage-worker"),
        ExecutionSubjectKind.Service,
        new AuthenticationEvidence(
            new AuthenticationEvidenceId("triage-worker-mi"),
            new IdentityIssuerId("azure-managed-identity"),
            "managed-identity",
            DateTimeOffset.UtcNow,
            null,
            new AuthenticationEvidenceFingerprint(new ContentHash("sha256:triage-worker"))),
        claims: [],
        delegationChain: [],
        IdentityAssuranceLevel.Strong,
        new IdentityVersion(1));

    AgentEngine CreateEngine(Ticket ticket)
    {
        var builder = AgentEngine.CreateBuilder()
            .UseOpenAI(config["OpenAI:ApiKey"]!, "gpt-4o-mini")
            .UseSqliteSessions("/var/lib/triage/sessions.db")
            .WithIdentity(WorkerIdentity)
            .WithInstructions(
                "You triage support tickets. Classify the ticket, draft a first reply, " +
                "and call update_ticket exactly once with category, priority, and draft.")
            .WithRequestSettings(LlmRequestSettings.Default with { Temperature = 0.2, MaxOutputTokens = 1_500 })
            .WithMaxTurns(4)
            .WithAttemptTimeout(TimeSpan.FromSeconds(60));

        builder.Services.AddSqliteSecurityGrantStore(new SqliteSecurityGrantStoreTarget(
            "/var/lib/triage/grants.db", GrantStoreInstanceId,
            SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations));
        builder.Services.AddSecurityAuditSink(
            new SecurityAuditSinkRegistration(AllAuditEventKinds, SecurityAuditDelivery.Required, providesDurableAcceptance: true),
            audit);
        builder.Services.AddSingleton<ISecurityPolicy, TriagePolicy>();

        builder.Services.AddSingleton<ITool>(new UpdateTicketTool(tickets, ticket.Id));
        builder.Services.Configure<AgentToolsOptions>(o => o.AllowedToolIds.Add(UpdateTicketTool.Id));

        return builder.Build();
    }
}
```

`TriagePolicy` allows session state (`StateRead`, `StateMutation`) and abstains
otherwise, so any tool that would need a file, process, or network is denied by
default. `UpdateTicketTool` is an ordinary `ITool` over `ITicketApi`; its
`ToolEffects` declare it as a mutation, and the allow-list names it explicitly
rather than using `AllowAllRegisteredTools`.

## Use it

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await foreach (var ticket in queue.ReadAllAsync(stoppingToken))
    {
        using var jobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        jobCancellation.CancelAfter(TimeSpan.FromMinutes(3));

        await using var engine = CreateEngine(ticket);
        var result = await engine.SendAsync(TicketPrompt(ticket), jobCancellation.Token);

        var usage = result.Events.OfType<ConversationUsageEvent>().Select(e => e.Usage).ToList();
        var completed = result.Events.OfType<ConversationTurnCompletedEvent>().Last();
        var updated = result.Events.OfType<ConversationToolResultEvent>()
            .Any(e => e.ToolName == "update_ticket" && e.Succeeded);

        logger.LogTriageCompleted(ticket.Id, completed.Outcome, updated,
            usage.Sum(u => u.InputTokens ?? 0), usage.Sum(u => u.OutputTokens ?? 0), usage.Sum(u => u.EstimatedCost ?? 0m));

        if (!updated)
        {
            await queue.DeadLetterAsync(ticket, completed.Outcome, stoppingToken);
        }
    }
}
```

`stoppingToken` flows into every turn, so host shutdown cancels the model call
and any in-flight tool, the turn ends with `Outcome == "cancelled"`, and the
ticket is dead-lettered instead of half-updated. The per-job `CancelAfter` is an
outer bound above the attempt timeout; both produce a typed outcome rather than
an exception from the engine.

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
  inventing a value.

## Status

The in-process path above is complete. Two designed capabilities that would
tighten this worker are not yet wired into the turn loop and are tracked in the
[implementation ledger](../implementation-progress.md#component-coverage):

- **Budgets.** `AgentKit.Budgets` provides hierarchical atomic reservations with
  typed exhaustion (`BudgetRejected` carrying a `BudgetLimitFailure`), and
  `AddInMemoryBudgetLedger` / `AddSqliteBudgetLedger` back it. The loop and
  providers do not reserve through it yet, so today a cost cap is the token
  arithmetic above plus `MaxOutputTokens` and `MaxTurns`.
- **Hosted engine.** `AddAgentKit()` with `AgentKitServiceProviderFactory`
  registers a host-managed engine that would let this worker keep one engine and
  many concurrent runs instead of one engine per job.

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
