# Storing conversations

An agent's conversation lives in a **session**: the ordered record of every user
message, model reply, and tool result. Where that record lives decides whether
it survives a restart. This guide covers the two first-party choices, in memory
and SQLite, and how to come back to a conversation later.

## Start in memory

`UseLocalDevelopmentDefaults()` keeps sessions in memory. The conversation works
exactly like a durable one while the process runs, and disappears with it. That
is the right choice for tests, scripts, and trying things out:

```csharp
await using var engine = AgentEngine.CreateBuilder()
    .UseLocalDevelopmentDefaults()
    .UseOpenAI(apiKey, "gpt-4o-mini")
    .Build();

await engine.AskAsync("Remember that my favourite colour is green.");
await engine.AskAsync("What is my favourite colour?");   // "Green." — same conversation
```

Each `AskAsync` continues the same session. Nothing is written to disk.

## Switch to SQLite

Add one line to keep conversations across restarts:

```csharp
await using var engine = AgentEngine.CreateBuilder()
    .UseLocalDevelopmentDefaults()
    .UseSqliteSessions("/var/lib/myapp/sessions.db")
    .UseOpenAI(apiKey, "gpt-4o-mini")
    .Build();
```

`UseSqliteSessions` registers the SQLite session store and directory, points the
session profile at them, and creates the database file (and its directory) on
first use. Everything else about the agent is unchanged. The path must be
absolute; put it somewhere the agent cannot edit, never inside a workspace you
gave it with `UseWorkspace`.

What becomes durable is the conversation: sessions, their messages, and the
directory that lists them. Security grants stay in memory, which is correct for
a local agent because a grant is single-use and scoped to one run.

## Come back to a conversation

A durable session is worth nothing if you cannot find it again.
`engine.Conversation` is the full `IConversationSession`, which lists and
resumes sessions for the current agent and user:

```csharp
// After a restart, with the same UseSqliteSessions path:
var page = await engine.Conversation.ListAsync(afterSessionId: null, maximumResults: 20);
if (page is ConversationSessionPage sessions && sessions.Sessions.Length > 0)
{
    var latest = sessions.Sessions[^1].SessionId;
    var opened = await engine.Conversation.OpenAsync(latest);
    if (opened is ConversationSessionOpened)
    {
        var history = await engine.Conversation.ReadHistoryAsync(new SessionSequence(0), maximumEntries: 200);
        // history is a ConversationHistoryPage of AgentMessage values; page until Complete is true
        Console.WriteLine(await engine.AskAsync("Where were we?"));
    }
}
```

To hand a client its own resume token without listing, read the identity the
first turn bound:

```csharp
var result = await engine.SendAsync("Hello");
var resumeToken = result.SessionId!.Value;            // also engine.Conversation.SessionId
```

A few rules keep this honest:

- **Open before you send.** A fresh engine starts a new session on its first
  `AskAsync`. Call `OpenAsync(sessionId)` first to continue an old one; once a
  conversation has been created or opened it cannot be rebound.
- **Same agent, same user.** `OpenAsync` refuses a session that belongs to a
  different agent identity or a different tenant/principal. The default agent
  identity is stable across restarts; if you set one, set it with `WithAgentId`
  and keep it.
- **History is paged.** `ReadHistoryAsync` counts stored entries, not messages,
  so a page can return fewer messages than you asked for. Follow `NextCursor`
  until `Complete` is `true`.

## Keep the database honest

The database file records the identity of the store that created it. By default
every engine built with `UseSqliteSessions` shares one identity, so any of your
applications can open a database another one created. Pass your own
`SqliteSessionStoreInstanceId` if two applications must never open each other's
files.

SQLite gives you durable local storage: committed work survives a crash, and two
processes on one machine can share the file safely. It does not give you
distributed leases, fencing across machines, or atomic writes across several
stores. The [sessions architecture](../architecture/sessions.md) describes what
a store must guarantee and what SQLite does and does not promise.

## Under the hood

The two lines above are these registrations, which a host that owns its own
`IServiceCollection` writes directly:

```csharp
var target = new SqliteSessionStoreTarget(
    "/var/lib/myapp/sessions.db",
    new SqliteSessionStoreInstanceId(myStableGuid),
    SqliteDatabaseOpenMode.CreateIfMissing,
    SqliteSchemaMode.ApplyKnownMigrations);

services.AddAgentSession();
services.AddSqliteSessionStore(target);
services.AddSqliteSessionDirectory(new ComponentId("myapp.session"), target);
```

Stores are additive and selected by the session profile's store key
(`agentkit.sqlite` here, `agentkit.in-memory` for the in-memory store), so
registering both is fine; registration order never chooses where data goes. The
directory is singular: one per composition. The
[CodingAgent example](../../examples/CodingAgent/README.md) shows this long form
together with a per-workspace database path.

Next: [Working with files](file-system.md) ·
[Permissions and approvals](permissions.md)
