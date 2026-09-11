// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

/// <summary>Verifies ModelUsage behavior and contracts.</summary>
public sealed class ModelUsageTests
{
    [Fact]
    public void ModelUsage_NotReported_HasNoReportedEvidence()
    {
        ModelUsage.NotReported.ReportState.ShouldBe(ModelUsageReportState.NotReported);
        ModelUsage.NotReported.InputTokens.ShouldBeNull();
        ModelUsage.NotReported.OutputTokens.ShouldBeNull();
        ModelUsage.NotReported.EstimatedCost.ShouldBeNull();
        ModelUsage.NotReported.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Theory]
    [InlineData(0, "inputTokens")]
    [InlineData(1, "outputTokens")]
    [InlineData(2, "cachedInputTokens")]
    [InlineData(3, "reasoningTokens")]
    [InlineData(4, "estimatedCost")]
    public void ModelUsage_WhenKnownValuesAreNegative_ThrowsArgumentOutOfRangeException(int field, string expectedParamName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ModelUsage(ModelUsageReportState.Final, field == 0 ? -1 : null, field == 1 ? -1 : null, field == 2 ? -1 : null, field == 3 ? -1 : null, field == 4 ? -1 : null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe(expectedParamName);
    }

    [Fact]
    public void ModelUsage_WhenCurrencyIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ModelUsage(ModelUsageReportState.Final, null, null, null, null, null, " ", ExtensionData.Empty));
        exception.ParamName.ShouldBe("costCurrency");
    }

    [Fact]
    public void ModelUsage_WhenNotReportedCarriesEvidence_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ModelUsage(ModelUsageReportState.NotReported, 0, null, null, null, null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("inputTokens");
    }

    [Theory]
    [InlineData(1, "outputTokens")]
    [InlineData(2, "cachedInputTokens")]
    [InlineData(3, "reasoningTokens")]
    [InlineData(4, "estimatedCost")]
    [InlineData(5, "costCurrency")]
    public void ModelUsage_WhenNotReportedCarriesOtherEvidence_ThrowsArgumentException(int field, string expectedParamName)
    {
        var exception = Should.Throw<ArgumentException>(() => new ModelUsage(ModelUsageReportState.NotReported, null, field == 1 ? 0 : null, field == 2 ? 0 : null, field == 3 ? 0 : null, field == 4 ? 0 : null, field == 5 ? "USD" : null, ExtensionData.Empty));
        exception.ParamName.ShouldBe(expectedParamName);
    }

    [Fact]
    public void ModelUsage_WhenReportStateIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ModelUsage((ModelUsageReportState) 99, null, null, null, null, null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("reportState");
    }

    [Fact]
    public void ModelUsage_WhenNotReportedCarriesExtensions_ThrowsArgumentException()
    {
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("provider.usage", new ExtensionValue([1])));
        var exception = Should.Throw<ArgumentException>(() => new ModelUsage(ModelUsageReportState.NotReported, null, null, null, null, null, null, extensions));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ModelUsage_WhenCostAndCurrencyAreReportedIndependently_PreservesBothShapes()
    {
        var costOnly = new ModelUsage(ModelUsageReportState.Interim, null, null, null, null, 0, null, ExtensionData.Empty);
        var currencyOnly = new ModelUsage(ModelUsageReportState.Final, null, null, null, null, null, "USD", ExtensionData.Empty);
        costOnly.EstimatedCost.ShouldBe(0);
        costOnly.CostCurrency.ShouldBeNull();
        currencyOnly.EstimatedCost.ShouldBeNull();
        currencyOnly.CostCurrency.ShouldBe("USD");
    }

    [Fact]
    public void ModelUsage_WhenSparseBoundaryValuesAreReported_PreservesStateAndValueSemantics()
    {
        var interim = new ModelUsage(ModelUsageReportState.Interim, 0, null, long.MaxValue, null, decimal.MaxValue, null, ExtensionData.Empty);
        var copy = interim with
        {
        };
        var final = new ModelUsage(ModelUsageReportState.Final, 0, null, long.MaxValue, null, decimal.MaxValue, null, ExtensionData.Empty);
        copy.ShouldBe(interim);
        copy.GetHashCode().ShouldBe(interim.GetHashCode());
        final.ShouldNotBe(interim);
        interim.ReportState.ShouldBe(ModelUsageReportState.Interim);
    }

    [Fact]
    public void ModelUsage_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ModelUsage(ModelUsageReportState.Final, 1, 1, null, null, null, null, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ModelUsage_Equality_WhenSameValues_InstancesAreEqual() => new ModelUsage(ModelUsageReportState.Final, 1, 2, null, null, null, null, ExtensionData.Empty).ShouldBe(new ModelUsage(ModelUsageReportState.Final, 1, 2, null, null, null, null, ExtensionData.Empty));
}
