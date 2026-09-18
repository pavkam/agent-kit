# QuickStart

The smallest complete AgentKit agent: one OpenAI model, local development
defaults, one instruction. It is the program the
[getting-started guide](../../docs/getting-started.md) walks through, kept in
the repository and covered by [QuickStart.Tests](../../tests/QuickStart.Tests)
so it always compiles and completes a turn against the current libraries.
[Example: QuickStart](../../docs/use-cases/quickstart-example.md) explains what
each line composes and the [use cases](../../docs/use-cases/index.md) grow it
into complete applications.

## Run it

```sh
export OPENAI_API_KEY=sk-...
dotnet run --project examples/QuickStart -- "In one sentence, what is AgentKit?"
```

## The whole agent

```csharp
await using var engine = AgentEngine.CreateBuilder()
    .UseLocalDevelopmentDefaults()
    .UseOpenAI(apiKey, "gpt-4o-mini")
    .WithInstructions("You are a concise assistant.")
    .Build();

Console.WriteLine(await engine.AskAsync(prompt));
```

[`AgentKit.Simple`](../../src/AgentKit.Simple/README.md) explains what each call
registers and how to add tools, other providers, durable sessions, and real
security policies through `builder.Services`. The
[CodingAgent](../CodingAgent/README.md) example is the long form of the same
composition with SQLite sessions, an approval broker, and eight tools.

Target: **.NET 10**.
