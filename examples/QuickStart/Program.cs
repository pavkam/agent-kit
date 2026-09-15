// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// The smallest complete AgentKit agent: one OpenAI model, in-memory session and security state,
// no tools. Set OPENAI_API_KEY, then `dotnet run --project examples/QuickStart -- "your question"`.
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("Set OPENAI_API_KEY before running the quick start.");
var prompt = args.Length > 0 ? string.Join(' ', args) : "In one sentence, what is AgentKit?";

using var conversation = QuickStartAgent.Create(apiKey);
var result = await conversation.SendAsync(prompt, CancellationToken.None);

foreach (var text in result.Events.OfType<ConversationAssistantTextEvent>())
{
    Console.WriteLine(text.Text);
}

return result.Succeeded ? 0 : 1;
