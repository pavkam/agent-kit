// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationRunnerConstructionTests
{
    [Fact]
    public async Task Constructor_WhenAnArgumentIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        await using var harness = await EvaluationHarness.CreateAsync();
        var services = harness.Engine.Services;
        var evaluators = services.GetRequiredService<IEvaluatorCatalog>();
        var stores = services.GetRequiredService<IEvaluationResultStoreSelector>();
        var exporters = services.GetRequiredService<IEvaluationReportExporterCatalog>();
        var ids = services.GetRequiredService<IIdentifierGenerator<EvaluationRunId>>();
        var options = new EvaluationOptionsSnapshot(1, 1, TimeSpan.FromSeconds(1));
        var logger = NullLogger.Instance;

        Should.Throw<ArgumentNullException>(() => new EvaluationRunner(null!, evaluators, stores, exporters, ids, harness.Time, options, logger)).ParamName.ShouldBe("engine");
        Should.Throw<ArgumentNullException>(() => new EvaluationRunner(harness.Engine, null!, stores, exporters, ids, harness.Time, options, logger)).ParamName.ShouldBe("evaluators");
        Should.Throw<ArgumentNullException>(() => new EvaluationRunner(harness.Engine, evaluators, null!, exporters, ids, harness.Time, options, logger)).ParamName.ShouldBe("stores");
        Should.Throw<ArgumentNullException>(() => new EvaluationRunner(harness.Engine, evaluators, stores, null!, ids, harness.Time, options, logger)).ParamName.ShouldBe("exporters");
        Should.Throw<ArgumentNullException>(() => new EvaluationRunner(harness.Engine, evaluators, stores, exporters, null!, harness.Time, options, logger)).ParamName.ShouldBe("evaluationRunIds");
        Should.Throw<ArgumentNullException>(() => new EvaluationRunner(harness.Engine, evaluators, stores, exporters, ids, null!, options, logger)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new EvaluationRunner(harness.Engine, evaluators, stores, exporters, ids, harness.Time, null!, logger)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentNullException>(() => new EvaluationRunner(harness.Engine, evaluators, stores, exporters, ids, harness.Time, options, null!)).ParamName.ShouldBe("logger");
    }

    [Fact]
    public async Task RunAsync_WhenTheGeneratorReturnsADefaultIdentity_FailsBeforeAnyEffect()
    {
        await using var harness = await EvaluationHarness.CreateAsync(
            services => services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<EvaluationRunId>>(new DefaultIds())));

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() => harness.Runner.RunAsync(EvaluationTestData.Plan(), TestContext.Current.CancellationToken));

        harness.Sessions.CreateRequests.ShouldBeEmpty();
    }

    private sealed class DefaultIds: IIdentifierGenerator<EvaluationRunId>
    {
        public EvaluationRunId Create() => default;
    }
}
