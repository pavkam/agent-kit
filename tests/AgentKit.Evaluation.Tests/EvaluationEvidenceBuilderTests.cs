// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationEvidenceBuilderTests
{
    private static UsageAccountingEntry Entry(
        ModelUsageReportState state = ModelUsageReportState.Final,
        long? input = 10,
        long? output = 12,
        bool model = true,
        string modelId = "model",
        string? deployment = null,
        int index = 1)
    {
        var runId = RunResultTestData.Run;
        return new UsageAccountingEntry(
            new UsageEntryId(new Guid(index, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1])),
            runId,
            new OperationId(Guid.NewGuid()),
            new UsageAccountingRevision(1),
            null,
            [],
            model ? new ModelUsageAttribution(new ModelRequestId(Guid.NewGuid()), new ProviderId("provider"), new ApiFamilyId("family"), new ModelId(modelId), deployment is null ? null : new DeploymentId(deployment)) : null,
            model ? new ModelUsage(state, state == ModelUsageReportState.NotReported ? null : input, state == ModelUsageReportState.NotReported ? null : output, null, null, null, null, ExtensionData.Empty) : null,
            ExtensionData.Empty);
    }

    private static RunUsage Usage(params UsageAccountingEntry[] entries) => new(RunResultTestData.Run, [.. entries]);

    [Fact]
    public void Usage_WhenTheRunMadeNoModelRequest_ReturnsNone()
    {
        EvaluationEvidenceBuilder.Usage(Usage()).ShouldBe(EvaluationUsageSummary.None);
        EvaluationEvidenceBuilder.Usage(Usage(Entry(model: false))).ShouldBe(EvaluationUsageSummary.None);
    }

    [Fact]
    public void Usage_WhenEveryRequestReportedFinalUsage_SumsTheTokens() =>
        EvaluationEvidenceBuilder.Usage(Usage(Entry(index: 1), Entry(input: 5, output: 7, index: 2))).ShouldBe(new EvaluationUsageSummary(2, 15, 19));

    [Fact]
    public void Usage_WhenAnyRequestHasNotReportedFinalUsage_LeavesBothTotalsUnknownInsteadOfPartial()
    {
        EvaluationEvidenceBuilder.Usage(Usage(Entry(index: 1), Entry(ModelUsageReportState.NotReported, index: 2))).ShouldBe(new EvaluationUsageSummary(2, null, null));
        EvaluationEvidenceBuilder.Usage(Usage(Entry(index: 1), Entry(ModelUsageReportState.Interim, index: 2))).ShouldBe(new EvaluationUsageSummary(2, null, null));
    }

    [Fact]
    public void Usage_WhenOneDimensionIsMissingFromAFinalReport_LeavesOnlyThatDimensionUnknown() =>
        EvaluationEvidenceBuilder.Usage(Usage(Entry(output: null))).ShouldBe(new EvaluationUsageSummary(1, 10, null));

    [Fact]
    public void Usage_WhenReportedZeroTokens_KeepsZeroDistinctFromUnknown() =>
        EvaluationEvidenceBuilder.Usage(Usage(Entry(input: 0, output: 0))).ShouldBe(new EvaluationUsageSummary(1, 0, 0));

    [Theory]
    [InlineData(typeof(RunSucceeded), "succeeded")]
    [InlineData(typeof(RunIdle), "idle")]
    public void OutcomeName_WhenOutcomeIsParameterless_ReturnsTheBoundedName(Type outcome, string expected) =>
        EvaluationEvidenceBuilder.OutcomeName((AgentRunOutcome) Activator.CreateInstance(outcome)!).ShouldBe(expected);

    [Fact]
    public void OutcomeName_WhenOutcomeCarriesEvidence_ReturnsTheBoundedName()
    {
        var error = RunResultTestData.Error(AgentErrorCodes.Cancelled);

        EvaluationEvidenceBuilder.OutcomeName(new RunCancelled(new CancellationReason(error))).ShouldBe("cancelled");
        EvaluationEvidenceBuilder.OutcomeName(new RunFailed(new RunFailure(error))).ShouldBe("failed");
        EvaluationEvidenceBuilder.OutcomeName(new RunDeferred([RunResultTestData.Deferred()])).ShouldBe("deferred");
    }

    [Fact]
    public void SettlementName_WhenSettlementIsKnown_ReturnsTheBoundedName()
    {
        EvaluationEvidenceBuilder.SettlementName(new RunSettlementCompleted()).ShouldBe("completed");
        EvaluationEvidenceBuilder.SettlementName(new RunSettlementRecoveryRequired(RunResultTestData.Error(AgentErrorCodes.Cancelled))).ShouldBe("recovery_required");
    }

    [Fact]
    public async Task Manifest_WhenUsageNamesRepeatedModels_ListsEachDistinctModelOnceInFirstUseOrder()
    {
        await using var harness = await EvaluationHarness.CreateAsync();
        var resolution = await harness.Engine.GetAgentAsync(EvaluationTestData.Agent, TestContext.Current.CancellationToken);
        var agent = resolution.ShouldBeOfType<ResolvedAgent>().Agent;

        var manifest = EvaluationEvidenceBuilder.Manifest(
            agent,
            Usage(Entry(modelId: "b", index: 1), Entry(modelId: "a", deployment: "dep", index: 2), Entry(modelId: "b", index: 3), Entry(model: false, index: 4)));

        manifest.ModelsUsed.ShouldBe(
        [
            new EvaluationModelUse("provider", "family", "b", null),
            new EvaluationModelUse("provider", "family", "a", "dep"),
        ]);
        manifest.AgentId.ShouldBe(agent.Id);
        manifest.DefinitionRevision.ShouldBe(agent.Definition.Revision);
        manifest.CatalogVersion.ShouldBe(agent.CatalogVersion);
        EvaluationEvidenceBuilder.Manifest(agent, null).ModelsUsed.ShouldBeEmpty();
    }
}
