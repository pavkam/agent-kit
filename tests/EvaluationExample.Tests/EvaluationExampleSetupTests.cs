// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace EvaluationExample.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies the documented evaluation composition builds, validates, runs its plan, records results, and exports a report.</summary>
public sealed class EvaluationExampleSetupTests
{
    private static AgentEngine Engine(HttpMessageHandler handler, StringWriter output)
    {
        var builder = EvaluationExampleSetup.CreateBuilder("sk-test", output);
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        return builder.Build();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void CreateBuilder_WhenApiKeyIsBlank_ThrowsArgumentExceptionBeforeComposing(string? apiKey) =>
        Should.Throw<ArgumentException>(() => EvaluationExampleSetup.CreateBuilder(apiKey!, TextWriter.Null)).ParamName.ShouldBe("apiKey");

    [Fact]
    public void CreateBuilder_WhenOutputIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => EvaluationExampleSetup.CreateBuilder("sk-test", null!)).ParamName.ShouldBe("output");

    [Fact]
    public async Task RunAsync_WhenTheAgentAnswersSafely_PassesEveryCaseRecordsThemAndExportsASummary()
    {
        var output = new StringWriter();
        await using var engine = Engine(new StubOpenAIHandler("Hello there!"), output);
        var plan = await EvaluationExampleSetup.CreatePlanAsync(engine, TestContext.Current.CancellationToken);

        var report = await engine.Services.GetRequiredService<IEvaluationRunner>().RunAsync(plan, TestContext.Current.CancellationToken);

        report.Status.ShouldBe(EvaluationReportStatus.Completed);
        report.Summary.ShouldBe(new EvaluationSummary(2, 0, 0, 0, 0));
        report.Results.Select(static r => r.CaseId.Value).ShouldBe(["greets", "stays-safe"]);
        var store = engine.Services.GetRequiredKeyedService<IEvaluationResultStore>(EvaluationExampleSetup.StoreKey.Value);
        var read = await store.ReadAsync(new EvaluationResultQuery(report.RunId, 10), TestContext.Current.CancellationToken);
        read.ShouldBeOfType<EvaluationResultsRead>().Results.ShouldBe(report.Results);
        output.ToString().ShouldContain("passed 2, failed 0");
        output.ToString().ShouldNotContain("Hello there!");
        _ = report.ExportResults.Single().Result.ShouldBeOfType<EvaluationExported>();
    }

    [Fact]
    public async Task RunAsync_WhenTheAgentLeaksTheCanary_FailsTheSafetyCheckAndReportsIt()
    {
        var output = new StringWriter();
        await using var engine = Engine(new StubOpenAIHandler("The token is SECRET-TOKEN."), output);
        var plan = await EvaluationExampleSetup.CreatePlanAsync(engine, TestContext.Current.CancellationToken);

        var report = await engine.Services.GetRequiredService<IEvaluationRunner>().RunAsync(plan, TestContext.Current.CancellationToken);

        report.Summary.ShouldBe(new EvaluationSummary(0, 2, 0, 0, 0));
        output.ToString().ShouldContain("safety=failed");
        output.ToString().ShouldNotContain("SECRET-TOKEN.");
    }
}
