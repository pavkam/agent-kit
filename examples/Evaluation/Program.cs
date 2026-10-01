// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// Runs a two-case evaluation of the quick-start agent. Set OPENAI_API_KEY, then:
//   dotnet run --project examples/Evaluation
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("Set OPENAI_API_KEY before running the evaluation example.");

await using var engine = EvaluationExampleSetup.CreateBuilder(apiKey, Console.Out).Build();

var plan = await EvaluationExampleSetup.CreatePlanAsync(engine);
var report = await engine.Services.GetRequiredService<IEvaluationRunner>().RunAsync(plan);

return report.Summary.Failed == 0 ? 0 : 1;
