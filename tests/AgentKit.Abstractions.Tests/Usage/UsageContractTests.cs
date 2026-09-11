// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Usage;

using System.Text.Json;

public sealed class UsageContractTests
{
    [Theory]
    [InlineData("dimension", "dimension")]
    [InlineData("unit", "unit")]
    [InlineData("aggregation", "aggregation")]
    [InlineData("default", "measurements")]
    [InlineData("null", "measurements")]
    [InlineData("foreign-dimension", "measurements")]
    [InlineData("foreign-unit", "measurements")]
    public void Constructor_WhenAggregateArgumentsAreInvalid_RejectsBeforeCalculating(string invalid, string parameter)
    {
        ImmutableArray<UsageMeasurement> measurements = invalid switch
        {
            "default" => default,
            "null" => [null!],
            "foreign-dimension" => [new(BudgetDimensions.OutputTokens, RunUsageTests.Tokens, null, UsageMeasurementQuality.Unknown)],
            "foreign-unit" => [new(BudgetDimensions.InputTokens, new BudgetUnit("other"), null, UsageMeasurementQuality.Unknown)],
            _ => [],
        };
        var exception = Should.Throw<ArgumentException>(() => new RunUsageAggregate(
            invalid == "dimension" ? default : BudgetDimensions.InputTokens,
            invalid == "unit" ? default : RunUsageTests.Tokens,
            invalid == "aggregation" ? (BudgetAggregationKind) (-1) : BudgetAggregationKind.Sum, measurements));
        exception.ParamName.ShouldBe(parameter);
        exception.GetType().ShouldBe(invalid == "null" ? typeof(ArgumentNullException)
            : invalid is "dimension" or "unit" or "aggregation" ? typeof(ArgumentOutOfRangeException) : typeof(ArgumentException));
    }

    [Fact]
    public void Constructor_WhenUsageIdentityIsEmpty_RejectsExactArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new UsageEntryId(Guid.Empty)).ParamName.ShouldBe("value");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenAccountingRevisionIsNotPositive_RejectsExactArgument(long value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new UsageAccountingRevision(value)).ParamName.ShouldBe("value");

    [Fact]
    public void Serialize_WhenUsageIdentityAndRevisionRoundTrip_PreservesCanonicalValuesAndRejectsInvalidJson()
    {
        var id = new UsageEntryId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        JsonSerializer.Deserialize<UsageEntryId>(JsonSerializer.Serialize(id)).ShouldBe(id);
        JsonSerializer.Deserialize<UsageAccountingRevision>(JsonSerializer.Serialize(new UsageAccountingRevision(long.MaxValue)))
            .Value.ShouldBe(long.MaxValue);
        id.ToString().ShouldBe("00000000-0000-0000-0000-000000000001");
        new UsageAccountingRevision(1).ToString().ShouldBe("1");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => JsonSerializer.Deserialize<UsageEntryId>("{\"Value\":\"00000000-0000-0000-0000-000000000000\"}"));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => JsonSerializer.Deserialize<UsageAccountingRevision>("{\"Value\":0}"));
    }

    [Theory]
    [InlineData(null, "source")]
    [InlineData("", "source")]
    [InlineData(" ", "source")]
    [InlineData(null, "version")]
    [InlineData("", "version")]
    [InlineData(" ", "version")]
    public void Constructor_WhenPricingProvenanceIsBlank_RejectsExactArgument(string? invalid, string parameter)
    {
        var exception = Should.Throw<ArgumentException>(() => new UsagePricingReference(parameter == "source" ? invalid! : "catalog", parameter == "version" ? invalid! : "v1"));
        exception.ParamName.ShouldBe(parameter);
        exception.GetType().ShouldBe(invalid is null ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Theory]
    [InlineData("dimension")]
    [InlineData("unit")]
    [InlineData("quality")]
    public void Constructor_WhenMeasurementIdentityOrQualityIsInvalid_RejectsExactArgument(string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new UsageMeasurement(
            parameter == "dimension" ? default : BudgetDimensions.InputTokens,
            parameter == "unit" ? default : RunUsageTests.Tokens, null,
            parameter == "quality" ? (UsageMeasurementQuality) (-1) : UsageMeasurementQuality.Unknown));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData(UsageMeasurementQuality.Measured, false)]
    [InlineData(UsageMeasurementQuality.ProviderReported, false)]
    [InlineData(UsageMeasurementQuality.Estimated, false)]
    [InlineData(UsageMeasurementQuality.Unknown, true)]
    [InlineData(UsageMeasurementQuality.NotApplicable, true)]
    public void Constructor_WhenAmountPresenceConflictsWithQuality_RejectsExactArgument(UsageMeasurementQuality quality, bool hasAmount) =>
        Should.Throw<ArgumentException>(() => new UsageMeasurement(BudgetDimensions.InputTokens, RunUsageTests.Tokens,
            hasAmount ? default(BudgetQuantity) : null, quality)).ParamName.ShouldBe("amount");

    [Fact]
    public void Constructor_WhenEstimatedCostHasNoPricing_RejectsInsteadOfInventingProvenance() =>
        Should.Throw<ArgumentNullException>(() => new UsageMeasurement(BudgetDimensions.Cost, new BudgetUnit("usd"), default(BudgetQuantity), UsageMeasurementQuality.Estimated))
            .ParamName.ShouldBe("pricing");

    [Fact]
    public void Constructor_WhenNonCostMeasurementCarriesPricing_RejectsExactArgument() =>
        Should.Throw<ArgumentException>(() => new UsageMeasurement(BudgetDimensions.InputTokens, RunUsageTests.Tokens, default(BudgetQuantity),
            UsageMeasurementQuality.Measured, new("catalog", "v1"))).ParamName.ShouldBe("pricing");

    [Theory]
    [InlineData("requestId")]
    [InlineData("providerId")]
    [InlineData("apiFamily")]
    [InlineData("modelId")]
    [InlineData("deploymentId")]
    public void Constructor_WhenModelAttributionIdentityIsDefault_RejectsExactArgument(string parameter)
    {
        var model = RunUsageTests.Model;
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ModelUsageAttribution(
            parameter == "requestId" ? default : model.RequestId,
            parameter == "providerId" ? default : model.ProviderId,
            parameter == "apiFamily" ? default : model.ApiFamily,
            parameter == "modelId" ? default : model.ModelId,
            parameter == "deploymentId" ? default(DeploymentId) : null));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("runId")]
    [InlineData("operationId")]
    [InlineData("revision")]
    [InlineData("previousRevision")]
    public void Constructor_WhenEntryIdentityOrRevisionIsDefault_RejectsExactArgument(string parameter)
    {
        var basis = RunUsageTests.Entry(1, []);
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new UsageAccountingEntry(
            parameter == "id" ? default : basis.Id, parameter == "runId" ? default : basis.RunId,
            parameter == "operationId" ? default : basis.OperationId, parameter == "revision" ? default : basis.Revision,
            parameter == "previousRevision" ? default(UsageAccountingRevision) : null, [], null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    public void Constructor_WhenEntryRevisionLinkageIsInvalid_RejectsExactArgument(long revision, long previous)
    {
        var basis = RunUsageTests.Entry(1, []);
        Should.Throw<ArgumentException>(() => new UsageAccountingEntry(basis.Id, basis.RunId, basis.OperationId,
            new(revision), previous == 0 ? null : new UsageAccountingRevision(previous), [], null, null, ExtensionData.Empty))
            .ParamName.ShouldBe("previousRevision");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenModelAndProviderReportPresenceDiffer_RejectsExactArgument(bool hasModel)
    {
        var basis = RunUsageTests.Entry(1, []);
        Should.Throw<ArgumentException>(() => new UsageAccountingEntry(basis.Id, basis.RunId, basis.OperationId, new(1), null,
            [], hasModel ? RunUsageTests.Model : null, hasModel ? null : ModelUsage.NotReported, ExtensionData.Empty))
            .ParamName.ShouldBe("providerUsage");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("null")]
    [InlineData("duplicate")]
    public void Constructor_WhenEntryMeasurementsAreInvalid_RejectsExactCollection(string kind)
    {
        ImmutableArray<UsageMeasurement> measurements = kind switch
        {
            "default" => default,
            "null" => [null!],
            _ => [RunUsageTests.Measure(1), RunUsageTests.Measure(2)],
        };
        var exception = Should.Throw<ArgumentException>(() => RunUsageTests.Entry(1, measurements));
        exception.ParamName.ShouldBe("measurements");
        exception.GetType().ShouldBe(kind == "null" ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Fact]
    public void Constructor_WhenEntryExtensionsAreNull_RejectsExactArgument()
    {
        var basis = RunUsageTests.Entry(1, []);
        Should.Throw<ArgumentNullException>(() => new UsageAccountingEntry(basis.Id, basis.RunId, basis.OperationId,
            new(1), null, [], null, null, null!)).ParamName.ShouldBe("extensions");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("null")]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    public void Constructor_WhenSnapshotEntriesAreInvalid_RejectsExactCollection(string kind)
    {
        var entry = RunUsageTests.Entry(1, []);
        ImmutableArray<UsageAccountingEntry> entries = kind switch
        {
            "default" => default,
            "null" => [null!],
            "duplicate" => [entry, entry],
            _ => [RunUsageTests.Entry(1, [], runId: new RunId(Guid.Parse("00000000-0000-0000-0000-000000000099")))],
        };
        var exception = Should.Throw<ArgumentException>(() => new RunUsage(RunUsageTests.Run, entries));
        exception.ParamName.ShouldBe("entries");
        exception.GetType().ShouldBe(kind == "null" ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Fact]
    public void Constructor_WhenSnapshotRunIsDefault_RejectsExactArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunUsage(default, [])).ParamName.ShouldBe("runId");

    [Fact]
    public void Apply_WhenEntryIsNull_RejectsBeforeCreatingProjection() =>
        Should.Throw<ArgumentNullException>(() => new RunUsage(RunUsageTests.Run, []).Apply(null!)).ParamName.ShouldBe("entry");

    [Theory]
    [InlineData("null")]
    [InlineData("dimension")]
    [InlineData("aggregation")]
    [InlineData("default-units")]
    [InlineData("empty-units")]
    [InlineData("default-unit")]
    [InlineData("duplicate-units")]
    [InlineData("unknown-unit")]
    [InlineData("selected-default")]
    public void GetAggregate_WhenDescriptorOrUnitIsInvalid_RejectsExactArgument(string invalid)
    {
        var basis = new BudgetDimensionDescriptor(BudgetDimensions.InputTokens, BudgetAggregationKind.Sum, [RunUsageTests.Tokens]);
        var descriptor = invalid switch
        {
            "null" => null,
            "dimension" => basis with { Dimension = default },
            "aggregation" => basis with { Aggregation = (BudgetAggregationKind) (-1) },
            "default-units" => basis with { AllowedUnits = default },
            "empty-units" => basis with { AllowedUnits = [] },
            "default-unit" => basis with { AllowedUnits = [default] },
            "duplicate-units" => basis with { AllowedUnits = [RunUsageTests.Tokens, RunUsageTests.Tokens] },
            _ => basis,
        };
        var unit = invalid == "selected-default" ? default : invalid == "unknown-unit" ? new BudgetUnit("different") : RunUsageTests.Tokens;
        var exception = Should.Throw<ArgumentException>(() => new RunUsage(RunUsageTests.Run, []).GetAggregate(descriptor!, unit));
        exception.ParamName.ShouldBe(invalid is "selected-default" or "unknown-unit" ? "unit" : "descriptor");
        exception.GetType().ShouldBe(invalid == "null" ? typeof(ArgumentNullException)
            : invalid is "dimension" or "aggregation" or "default-unit" or "selected-default" ? typeof(ArgumentOutOfRangeException) : typeof(ArgumentException));
    }
}
