// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

public sealed class ToolResultEvidenceTests
{
    [Fact]
    public void ToolResultRejectionPolicyKey_ToString_WhenDefault_ReturnsEmptyText() =>
        default(ToolResultRejectionPolicyKey).ToString().ShouldBe(string.Empty);

    [Fact]
    public void ToolUsageMeasurement_Constructor_WhenQualityAndAmountConflict_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolUsageMeasurement(
            new BudgetDimension("requests"), new BudgetUnit("count"), null,
            ToolUsageMeasurementQuality.Measured));

        exception.ParamName.ShouldBe("amount");
    }

    [Fact]
    public void ToolUsage_Constructor_WhenDimensionAndUnitDuplicate_ThrowsExactException()
    {
        var first = new ToolUsageMeasurement(
            new BudgetDimension("requests"), new BudgetUnit("count"), BudgetQuantity.FromDecimal(1),
            ToolUsageMeasurementQuality.Measured);
        var second = new ToolUsageMeasurement(
            first.Dimension, first.Unit, BudgetQuantity.FromDecimal(2), ToolUsageMeasurementQuality.Estimated);

        var exception = Should.Throw<ArgumentException>(() => new ToolUsage([first, second], ExtensionData.Empty));

        exception.ParamName.ShouldBe("measurements");
    }

    [Fact]
    public void ToolUsage_Equality_WhenEquivalentArraysDifferByInstance_IsStructural()
    {
        var measurement = new ToolUsageMeasurement(
            new BudgetDimension("requests"), new BudgetUnit("count"), BudgetQuantity.FromDecimal(1),
            ToolUsageMeasurementQuality.Measured);
        var first = new ToolUsage([measurement], ExtensionData.Empty);
        var same = new ToolUsage([measurement], ExtensionData.Empty);
        var empty = new ToolUsage([], ExtensionData.Empty);

        first.ShouldBe(same);
        first.GetHashCode().ShouldBe(same.GetHashCode());
        first.ShouldNotBe(empty);
    }

    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenMeasurementsUnknown_PreservesNull()
    {
        var info = new ToolResultNormalizationInfo(
            [ToolResultNormalizationTransformation.Redacted], null, null, null, null, ExtensionData.Empty);

        info.InputCanonicalBytes.ShouldBeNull();
        info.OmittedCanonicalBytes.ShouldBeNull();
    }

    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenTransformationDuplicate_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolResultNormalizationInfo(
            [ToolResultNormalizationTransformation.Redacted, ToolResultNormalizationTransformation.Redacted],
            null, null, null, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("transformations");
    }

    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenTransformationUndefined_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultNormalizationInfo(
            [(ToolResultNormalizationTransformation) 99], null, null, null, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("transformations");
    }

    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenOmittedExceedsInput_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultNormalizationInfo(
            [], 2, null, 3, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("omittedCanonicalBytes");
    }

    [Fact]
    public void ToolError_Constructor_WhenRetryDelayNegative_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolError(
            ToolErrorKind.Tool, "safe", null, TimeSpan.FromTicks(-1), ExtensionData.Empty));

        exception.ParamName.ShouldBe("retryAfter");
    }

    [Fact]
    public void ToolCallAdmissionEvidence_Constructor_WhenOrdinalNegative_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallAdmissionEvidence(
            new ToolCatalogVersion("catalog"), -1, new InputFingerprint("sha256:raw")));

        exception.ParamName.ShouldBe("sourceOrdinal");
    }

    [Fact]
    public void ToolCallAcceptanceEvidence_Constructor_WhenFingerprintDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallAcceptanceEvidence(
            new GrantId(Guid.NewGuid()), default, DateTimeOffset.UnixEpoch));

        exception.ParamName.ShouldBe("validatedArgumentsFingerprint");
    }

    [Fact]
    public void ToolUsage_Constructor_WhenArrayDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ToolUsage(default, ExtensionData.Empty));

        exception.ParamName.ShouldBe("measurements");
    }

    [Fact]
    public void ToolResultBounds_Constructor_WhenBoundaryPositive_RetainsLimits()
    {
        var bounds = new ToolResultBounds(1, 1);

        bounds.MaximumCanonicalBytes.ShouldBe(1);
        bounds.MaximumParts.ShouldBe(1);
    }

    [Fact]
    public void ToolResultNormalizationSnapshot_Constructor_WhenFlagsUnknown_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultNormalizationSnapshot(
            RejectionPolicy(), ProjectionPolicy(), null, new ToolResultNormalizationAlgorithmVersion(1),
            new ToolResultBounds(1, 1), (ToolResultProjectionTransformations) (1 << 20), ExtensionData.Empty));

        exception.ParamName.ShouldBe("allowedTransformations");
    }

    [Fact]
    public void ToolResultNormalizationSnapshot_Constructor_WhenRequiredPolicyNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultNormalizationSnapshot(
            null!, ProjectionPolicy(), null, new ToolResultNormalizationAlgorithmVersion(1),
            new ToolResultBounds(1, 1), ToolResultProjectionTransformations.None, ExtensionData.Empty));

        exception.ParamName.ShouldBe("rejectionPolicy");
    }

    private static ToolResultProjectionPolicyReference ProjectionPolicy() => new(
        new ToolResultProjectionPolicyKey("projection"), new ToolResultProjectionPolicyVersion(1));

    private static ToolResultRejectionPolicyReference RejectionPolicy() => new(
        new ToolResultRejectionPolicyKey("rejection"), new ToolResultRejectionPolicyVersion(1));
}
