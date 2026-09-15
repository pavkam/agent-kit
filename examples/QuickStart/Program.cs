// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// The smallest complete AgentKit agent. Set OPENAI_API_KEY, then:
//   dotnet run --project examples/QuickStart -- "your question"
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("Set OPENAI_API_KEY before running the quick start.");
var prompt = args.Length > 0 ? string.Join(' ', args) : "In one sentence, what is AgentKit?";

await using var engine = QuickStartAgent.CreateBuilder(apiKey).Build();

Console.WriteLine(await engine.AskAsync(prompt));
