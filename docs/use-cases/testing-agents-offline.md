# Testing an agent without a live model

A team ships an agent as part of their product and wants it under the same
continuous integration as the rest of the code: fast, deterministic, no API key
in the build, no network. They need to prove that the composition builds, that
the right instructions and tools reach the wire, that a tool call round trip
works against a file system they control, that denials happen before effects,
and that provider failures never leak secrets.

## What the tests need

| Need                                   | AgentKit part                                                                         |
| -------------------------------------- | ------------------------------------------------------------------------------------- |
| No network                             | Replace the provider's `HttpClient` with one built over your `HttpMessageHandler`     |
| Scripted model replies                 | A handler that returns canned Chat Completions responses, including tool calls        |
| A file system you can seed and inspect | `AddInMemoryFileSystem` from `AgentKit.FileSystem.InMemory` plus the read/write tools |
| Deterministic time                     | `builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(fakeTime))`       |
| Assertions on behavior                 | `ConversationTurnResult.Events` and the requests your handler captured                |

## Compose the engine under test

Keep the production builder in one method the tests can reach, then substitute
only the boundaries you need. This is exactly what
[`tests/QuickStart.Tests`](../../tests/QuickStart.Tests/QuickStartAgentTests.cs)
does for the quick start:

```csharp
static AgentEngine TestEngine(HttpMessageHandler handler, Action<AgentEngineBuilder>? adjust = null)
{
    var builder = AgentEngine.CreateBuilder()
        .UseLocalDevelopmentDefaults()
        .UseOpenAI("sk-test", "gpt-4o-mini", o => o.PreferStreaming = false)
        .WithInstructions("You are a concise assistant.");

    // The in-memory file system replaces the sandbox; the tools stay the same.
    builder.Services.AddInMemoryFileSystem();
    builder.Services.AddReadTool();
    builder.Services.AddWriteTool();

    // No sockets: every provider request goes to the handler.
    builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));

    adjust?.Invoke(builder);
    return builder.Build();
}
```

A stub handler records what it received and answers from a queue. The quick
start's `StubOpenAIHandler` returns one text reply; a version that can also
answer with a tool call looks like this:

```csharp
sealed class ScriptedOpenAIHandler(params string[] responseBodies) : HttpMessageHandler
{
    readonly Queue<string> _responses = new(responseBodies);
    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json"),
        };
    }

    public static string Text(string content) => $$"""
        {"id":"chatcmpl-1","object":"chat.completion","model":"gpt-4o-mini",
         "choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":{{JsonSerializer.Serialize(content)}}}}],
         "usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}
        """;

    public static string ToolCall(string callId, string name, string argumentsJson) => $$"""
        {"id":"chatcmpl-2","object":"chat.completion","model":"gpt-4o-mini",
         "choices":[{"index":0,"finish_reason":"tool_calls","message":{"role":"assistant","content":null,
           "tool_calls":[{"id":"{{callId}}","type":"function","function":{"name":"{{name}}","arguments":{{JsonSerializer.Serialize(argumentsJson)}}}}]}}],
         "usage":{"prompt_tokens":12,"completion_tokens":8,"total_tokens":20}}
        """;
}
```

The adapter streams by default, which is why `TestEngine` sets
`PreferStreaming = false`: the bodies above are plain Chat Completions
responses. To exercise the streaming path instead, return `text/event-stream`
frames the way the quick start's `StubOpenAIHandler` does; the adapter's own
tests cover both paths at arbitrary fragmentation boundaries.

## Write the tests

The composition builds and validates without a network call:

```csharp
[Fact]
public async Task Build_WhenComposed_ValidatesWithoutTouchingTheNetwork()
{
    await using var engine = TestEngine(new ThrowingHandler());

    (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
}
```

A tool call round trip against seeded content, asserting on the events and on
what the model was told:

```csharp
[Fact]
public async Task SendAsync_WhenTheModelReadsAFile_ReturnsTheContentAndCorrelatesTheCall()
{
    var handler = new ScriptedOpenAIHandler(
        ScriptedOpenAIHandler.ToolCall("call_1", "read_file", """{"path":"notes/todo.md"}"""),
        ScriptedOpenAIHandler.Text("Your first item is 'write tests'."));
    await using var engine = TestEngine(handler);
    engine.Services.GetRequiredService<InMemoryFileSystem>().Seed(new FileSystemPath("notes/todo.md"), "- write tests\n");

    var result = await engine.SendAsync("What is first on my list?", TestContext.Current.CancellationToken);

    result.Succeeded.ShouldBeTrue();
    var call = result.Events.OfType<ConversationToolCallEvent>().ShouldHaveSingleItem();
    var toolResult = result.Events.OfType<ConversationToolResultEvent>().ShouldHaveSingleItem();
    call.ToolName.ShouldBe("read_file");
    toolResult.CallId.ShouldBe(call.CallId);
    toolResult.Succeeded.ShouldBeTrue();
    handler.RequestBodies[1].ShouldContain("write tests");          // the tool result reached the model
    handler.RequestBodies[0].ShouldContain("\"name\":\"read_file\""); // the tool was offered
}
```

Denial happens before the effect:

```csharp
[Fact]
public async Task SendAsync_WhenPolicyDeniesWrites_LeavesTheFileSystemUntouched()
{
    var handler = new ScriptedOpenAIHandler(
        ScriptedOpenAIHandler.ToolCall("call_1", "write_file", """{"path":"out.txt","content":"x","mode":"create_only"}"""),
        ScriptedOpenAIHandler.Text("I was not allowed to write that."));
    await using var engine = TestEngine(handler, b => b.Services.AddSingleton<ISecurityPolicy, ReadOnlyWorkspacePolicy>());

    var result = await engine.SendAsync("Create out.txt", TestContext.Current.CancellationToken);

    result.Events.OfType<ConversationToolResultEvent>().ShouldHaveSingleItem().Succeeded.ShouldBeFalse();
    handler.RequestBodies[1].ShouldContain("may only read");        // the model saw the safe denial message
}
```

Failures carry no secrets:

```csharp
[Fact]
public async Task AskAsync_WhenTheProviderIsUnreachable_ThrowsWithoutTheKey()
{
    await using var engine = TestEngine(new ThrowingHandler());

    var exception = await Should.ThrowAsync<SimpleAgentException>(() => engine.AskAsync("hello", TestContext.Current.CancellationToken));

    exception.Message.ShouldNotContain("sk-test");
    exception.Result.Succeeded.ShouldBeFalse();
}
```

## Other boundaries you can script

- **Processes.** `AgentKit.Processes.Scripted` (`AddScriptedProcesses`) stands
  in for the operating-system runner with deterministic scenarios, so a test of
  the `command` tool never starts a real process.
- **Network.** `AgentKit.Network.InMemory` (`AddAgentNetworkInMemory`) provides
  a scripted resolver and transport for `web_fetch`.
- **Language services.** `AgentKit.LanguageServices.Scripted` backs the
  `language` tool with canned results.
- **Sessions.** The in-memory session store from the local defaults is already
  deterministic; to test persistence, point `UseSqliteSessions` at a temporary
  file and reopen it in a second engine.

## What the framework guarantees

- **Unit tests never need credentials.** Every provider adapter takes its
  `HttpClient` from the container; every host boundary has an in-memory or
  scripted leaf.
- **Events are the observable contract.** Text, tool calls, tool results, usage,
  and completion arrive as typed events in commit order, so tests assert on
  behavior rather than on internals.
- **The same tools run in production and in tests.** Only the file system,
  process runner, or transport underneath them changes, which is what keeps a
  green test meaningful.

## What lives where

| Concern                       | Package                                                                          |
| ----------------------------- | -------------------------------------------------------------------------------- |
| In-memory file system         | [AgentKit.FileSystem.InMemory](../../src/AgentKit.FileSystem.InMemory/README.md) |
| Scripted processes            | [AgentKit.Processes.Scripted](../../src/AgentKit.Processes.Scripted/README.md)   |
| In-memory network             | [AgentKit.Network.InMemory](../../src/AgentKit.Network.InMemory/README.md)       |
| Shared fakes and fixtures     | [AgentKit.Test.Shared](../../tests/AgentKit.Test.Shared/README.md)               |
| Reusable contract suites      | [AgentKit.Conformance](../../tests/AgentKit.Conformance/README.md)               |
| How to run tests in this repo | [Testing guide](../testing/index.md)                                             |

Next: [Use cases](index.md) · [Example: QuickStart](quickstart-example.md)
