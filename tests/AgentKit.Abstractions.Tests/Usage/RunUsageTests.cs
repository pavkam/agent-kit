// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Usage;

using System.Numerics;

public sealed class RunUsageTests
{
    internal static readonly RunId Run = new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    internal static readonly OperationId Operation = new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    internal static readonly BudgetUnit Tokens = new("tokens");
    internal static readonly ModelUsageAttribution Model = new(new ModelRequestId(Guid.Parse("50000000-0000-0000-0000-000000000001")),
        new ProviderId("provider"), new ApiFamilyId("family"), new ModelId("model"));

    [Fact]
    public void Apply_WhenInterimIsReplacedByFinalAndCorrected_CountsLatestEvidenceOnceAndPreservesPriorSnapshots()
    {
        var interim = Entry(1, [Measure(20, UsageMeasurementQuality.Estimated)], reportState: ModelUsageReportState.Interim);
        var original = new RunUsage(Run, [interim]);
        var terminal = Entry(1, [Measure(12)], revision: 2, reportState: ModelUsageReportState.Final);
        var final = original.Apply(terminal);
        var corrected = final.Apply(Entry(1, [Measure(9)], revision: 3, reportState: ModelUsageReportState.Final));

        Total(original).Amount.ShouldBe(BudgetQuantity.FromDecimal(20));
        Total(original).HasEstimates.ShouldBeTrue();
        Total(final).Amount.ShouldBe(BudgetQuantity.FromDecimal(12));
        Total(corrected).Amount.ShouldBe(BudgetQuantity.FromDecimal(9));
        Total(corrected).HasEstimates.ShouldBeFalse();
        corrected.Entries.ShouldHaveSingleItem().Revision.Value.ShouldBe(3);
        corrected.Entries[0].PreviousRevision!.Value.Value.ShouldBe(2);
        final.Apply(Entry(1, [Measure(12)], revision: 2, reportState: ModelUsageReportState.Final)).ShouldBeSameAs(final);
    }

    [Fact]
    public void Apply_WhenRetrySharesRequestAndOperation_RetainsBothChargedAttemptsInFirstSeenOrder()
    {
        var first = Entry(1, [Measure(7)]);
        var retry = Entry(2, [Measure(11)]);
        var usage = new RunUsage(Run, []).Apply(first).Apply(retry);
        var corrected = usage.Apply(Entry(1, [Measure(4)], revision: 2));

        Total(usage).Amount.ShouldBe(BudgetQuantity.FromDecimal(18));
        Total(corrected).Amount.ShouldBe(BudgetQuantity.FromDecimal(15));
        corrected.Entries.Select(static entry => entry.Id).ShouldBe([first.Id, retry.Id]);
        corrected.Entries.ShouldAllBe(entry => entry.Model == Model && entry.OperationId == Operation);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("not-applicable")]
    [InlineData("zero")]
    public void GetAggregate_WhenOneAttemptLacksKnownUsage_DistinguishesMissingUnknownInapplicableAndZero(string scenario)
    {
        ImmutableArray<UsageMeasurement> observations = scenario switch
        {
            "missing" => [],
            "unknown" => [new(BudgetDimensions.InputTokens, Tokens, null, UsageMeasurementQuality.Unknown)],
            "not-applicable" => [new(BudgetDimensions.InputTokens, Tokens, null, UsageMeasurementQuality.NotApplicable)],
            _ => [Measure(0)],
        };
        var usage = new RunUsage(Run, [Entry(1, [Measure(8)]), Entry(2, observations)]);

        var total = Total(usage);

        total.KnownAmount.ShouldBe(BudgetQuantity.FromDecimal(8));
        if (scenario is "missing" or "unknown") { total.Amount.ShouldBeNull(); }
        else { total.Amount.ShouldBe(BudgetQuantity.FromDecimal(8)); }
    }

    [Fact]
    public void GetAggregate_WhenNoApplicableEvidenceExists_DoesNotFabricateZero()
    {
        Total(new RunUsage(Run, [])).Amount.ShouldBeNull();
        Total(new RunUsage(Run, [Entry(1, [new(BudgetDimensions.InputTokens, Tokens, null, UsageMeasurementQuality.NotApplicable)])])).Amount.ShouldBeNull();
    }

    [Theory]
    [InlineData(BudgetAggregationKind.Sum, 17)]
    [InlineData(BudgetAggregationKind.Duration, 17)]
    [InlineData(BudgetAggregationKind.Maximum, 12)]
    [InlineData(BudgetAggregationKind.ConcurrentGauge, 17)]
    public void GetAggregate_WhenAggregationIsDeclared_AppliesExactCurrentObservationSemantics(BudgetAggregationKind aggregation, int expected)
    {
        var usage = new RunUsage(Run, [Entry(1, [Measure(5)]), Entry(2, [Measure(12)])]);
        var total = usage.GetAggregate(new(BudgetDimensions.InputTokens, aggregation, [Tokens]), Tokens);
        total.Amount.ShouldBe(BudgetQuantity.FromDecimal(expected));
        total.Aggregation.ShouldBe(aggregation);
    }

    [Fact]
    public void GetAggregate_WhenGaugeCompletes_RequiresExplicitZeroReplacement()
    {
        var descriptor = new BudgetDimensionDescriptor(BudgetDimensions.InputTokens, BudgetAggregationKind.ConcurrentGauge, [Tokens]);
        var usage = new RunUsage(Run, [Entry(1, [Measure(3)])]);
        usage.GetAggregate(descriptor, Tokens).Amount.ShouldBe(BudgetQuantity.FromDecimal(3));
        usage.Apply(Entry(1, [Measure(0)], revision: 2)).GetAggregate(descriptor, Tokens).Amount.ShouldBe(default(BudgetQuantity));
    }

    [Fact]
    public void GetAggregate_WhenValuesExceedDecimalRange_PreservesExactSumAndMixedQualities()
    {
        var huge = new BudgetQuantity(BigInteger.Parse("999999999999999999999999999999999999999", System.Globalization.CultureInfo.InvariantCulture), 0);
        var usage = new RunUsage(Run,
        [
            Entry(1, [new(BudgetDimensions.InputTokens, Tokens, huge, UsageMeasurementQuality.ProviderReported)]),
            Entry(2, [Measure(1, UsageMeasurementQuality.Measured)]),
            Entry(3, [Measure(2, UsageMeasurementQuality.Estimated)]),
        ]);

        var aggregate = Total(usage);

        aggregate.Amount.ShouldBe(huge.Add(BudgetQuantity.FromDecimal(3)));
        aggregate.HasEstimates.ShouldBeTrue();
        aggregate.Measurements.Select(static value => value.Quality).ShouldBe([
            UsageMeasurementQuality.ProviderReported, UsageMeasurementQuality.Measured, UsageMeasurementQuality.Estimated]);
    }

    [Fact]
    public void GetAggregate_WhenCostsUseDifferentCurrencies_KeepsUnitsAndPricingProvenanceSeparate()
    {
        var usd = new BudgetUnit("usd");
        var eur = new BudgetUnit("eur");
        var pricing = new UsagePricingReference("test pricing catalog", "revision-4");
        var usage = new RunUsage(Run,
        [
            Entry(1, [new(BudgetDimensions.Cost, usd, BudgetQuantity.FromDecimal(1.2m), UsageMeasurementQuality.Estimated, pricing)]),
            Entry(2, [new(BudgetDimensions.Cost, eur, BudgetQuantity.FromDecimal(2.3m), UsageMeasurementQuality.ProviderReported)]),
        ]);
        var descriptor = new BudgetDimensionDescriptor(BudgetDimensions.Cost, BudgetAggregationKind.Sum, [usd, eur]);

        var dollars = usage.GetAggregate(descriptor, usd);
        var euros = usage.GetAggregate(descriptor, eur);

        dollars.Amount.ShouldBe(BudgetQuantity.FromDecimal(1.2m));
        euros.Amount.ShouldBe(BudgetQuantity.FromDecimal(2.3m));
        dollars.Measurements[0].Pricing.ShouldBe(pricing);
        dollars.HasEstimates.ShouldBeTrue();
        euros.HasEstimates.ShouldBeFalse();
    }

    [Fact]
    public void GetAggregate_WhenTokenCategoriesOverlap_RetainsIndependentDimensionsWithoutInventingTotalTokens()
    {
        var dimensions = new[] { BudgetDimensions.InputTokens, BudgetDimensions.OutputTokens, BudgetDimensions.ReasoningTokens,
            BudgetDimensions.CachedReadTokens, BudgetDimensions.CachedWriteTokens };
        var measurements = dimensions.Select((dimension, index) => new UsageMeasurement(dimension, Tokens, BudgetQuantity.FromDecimal(index + 1), UsageMeasurementQuality.ProviderReported)).ToImmutableArray();
        var usage = new RunUsage(Run, [Entry(1, measurements)]);

        for (var index = 0; index < dimensions.Length; index++)
        {
            usage.GetAggregate(new(dimensions[index], BudgetAggregationKind.Sum, [Tokens]), Tokens).Amount.ShouldBe(BudgetQuantity.FromDecimal(index + 1));
        }
    }

    [Theory]
    [InlineData("run")]
    [InlineData("new-revision")]
    [InlineData("skipped-revision")]
    [InlineData("conflicting-replay")]
    [InlineData("old-revision")]
    [InlineData("operation")]
    [InlineData("model")]
    [InlineData("report-regression")]
    public void Apply_WhenUpdateConflicts_RejectsWithoutChangingOriginalEvidence(string conflict)
    {
        var current = Entry(1, [Measure(4)], revision: 2, reportState: ModelUsageReportState.Final);
        var usage = new RunUsage(Run, [current]);
        var changedModel = new ModelUsageAttribution(Model.RequestId, Model.ProviderId, Model.ApiFamily, new ModelId("changed"));
        var update = Entry(conflict == "new-revision" ? 2 : 1, [Measure(9)],
            revision: conflict == "old-revision" ? 1 : conflict is "new-revision" or "conflicting-replay" ? 2 : conflict == "skipped-revision" ? 4 : 3,
            runId: conflict == "run" ? new RunId(Guid.Parse("30000000-0000-0000-0000-000000000099")) : Run,
            operation: conflict == "operation" ? new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000099")) : Operation,
            model: conflict == "model" ? changedModel : Model,
            reportState: conflict == "report-regression" ? ModelUsageReportState.Interim : ModelUsageReportState.Final);

        var exception = Should.Throw<ArgumentException>(() => usage.Apply(update));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("entry");
        usage.Entries.ShouldHaveSingleItem().ShouldBeSameAs(current);
        Total(usage).Amount.ShouldBe(BudgetQuantity.FromDecimal(4));
    }

    [Fact]
    public void Equals_WhenArraysAreIndependentlyAllocated_UsesStructuralEvidenceAndCompatibleHashes()
    {
        var first = new RunUsage(Run, [Entry(1, [Measure(3)])]);
        var second = new RunUsage(Run, [Entry(1, [Measure(3)])]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.Entries[0].GetHashCode().ShouldBe(second.Entries[0].GetHashCode());
        Total(first).ShouldBe(Total(second));
        Total(first).GetHashCode().ShouldBe(Total(second).GetHashCode());
        (first with { }).ShouldBe(first);
    }

    [Fact]
    public void Apply_WhenProviderRetainsNativeUsageAndAdjustmentEvidence_PreservesBothWithoutReconstructingTranscript()
    {
        var native = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("provider.cached_write", new([49, 50])));
        var adjustment = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("billing.correction", new([116, 114, 117, 101])));
        var basis = Entry(1, [Measure(3)]);
        var report = new ModelUsage(ModelUsageReportState.Final, 3, 4, 2, 1, null, "usd", native);
        var entry = new UsageAccountingEntry(basis.Id, Run, Operation, new(2), new UsageAccountingRevision(1),
            [Measure(3)], Model, report, adjustment);

        var updated = new RunUsage(Run, [basis]).Apply(entry);

        updated.Entries[0].ProviderUsage.ShouldBeSameAs(report);
        updated.Entries[0].ProviderUsage!.Extensions.ShouldBe(native);
        updated.Entries[0].Extensions.ShouldBe(adjustment);
        updated.Entries[0].ProviderUsage!.CostCurrency.ShouldBe("usd");
    }

    [Fact]
    public void Apply_WhenInterimReportBecomesAbsent_RejectsRegressionWithoutLosingPriorUsage()
    {
        var usage = new RunUsage(Run, [Entry(1, [], reportState: ModelUsageReportState.Interim)]);
        Should.Throw<ArgumentException>(() => usage.Apply(Entry(1, [], revision: 2))).ParamName.ShouldBe("entry");
        usage.Entries[0].ProviderUsage!.ReportState.ShouldBe(ModelUsageReportState.Interim);
    }

    internal static UsageMeasurement Measure(decimal amount, UsageMeasurementQuality quality = UsageMeasurementQuality.ProviderReported) =>
        new(BudgetDimensions.InputTokens, Tokens, BudgetQuantity.FromDecimal(amount), quality);

    internal static UsageAccountingEntry Entry(int id, ImmutableArray<UsageMeasurement> measurements, long revision = 1,
        RunId? runId = null, OperationId? operation = null, ModelUsageAttribution? model = null, ModelUsageReportState reportState = ModelUsageReportState.NotReported) =>
        new(new UsageEntryId(new Guid(id, 0, 0, new byte[8])), runId ?? Run, operation ?? Operation,
            new UsageAccountingRevision(revision), revision == 1 ? null : new UsageAccountingRevision(revision - 1),
            measurements, model ?? Model, new ModelUsage(reportState, null, null, null, null, null, null, ExtensionData.Empty), ExtensionData.Empty);

    private static RunUsageAggregate Total(RunUsage usage) => usage.GetAggregate(new(BudgetDimensions.InputTokens, BudgetAggregationKind.Sum, [Tokens]), Tokens);
}
