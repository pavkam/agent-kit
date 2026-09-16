// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolResultNormalizationInfo behavior and contracts.</summary>
public sealed class ToolResultNormalizationInfoTests
{
    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenMeasurementsUnknown_PreservesNull()
    {
        var info = new ToolResultNormalizationInfo([ToolResultNormalizationTransformation.Redacted], null, null, null, null, ExtensionData.Empty);
        info.InputCanonicalBytes.ShouldBeNull();
        info.OmittedCanonicalBytes.ShouldBeNull();
    }

    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenTransformationDuplicate_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolResultNormalizationInfo([ToolResultNormalizationTransformation.Redacted, ToolResultNormalizationTransformation.Redacted], null, null, null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("transformations");
    }

    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenTransformationUndefined_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultNormalizationInfo([(ToolResultNormalizationTransformation) 99], null, null, null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("transformations");
    }

    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenOmittedExceedsInput_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultNormalizationInfo([], 2, null, 3, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("omittedCanonicalBytes");
    }

    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenInputPartsIsNegative_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultNormalizationInfo([], null, -1, null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("inputParts");
    }

    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenOmittedPartsIsNegative_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultNormalizationInfo([], null, null, null, -1, ExtensionData.Empty));
        exception.ParamName.ShouldBe("omittedParts");
    }

    [Fact]
    public void ToolResultNormalizationInfo_Constructor_WhenOmittedPartsExceedsInputParts_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultNormalizationInfo([], null, 2, null, 3, ExtensionData.Empty));
        exception.ParamName.ShouldBe("omittedParts");
    }

    [Fact]
    public void Equality_WhenEquivalentTransformationsDifferByInstance_IsStructurallyEqual()
    {
        var left = new ToolResultNormalizationInfo([ToolResultNormalizationTransformation.Redacted], 10, 2, 1, 1, ExtensionData.Empty);
        var right = new ToolResultNormalizationInfo([ToolResultNormalizationTransformation.Redacted], 10, 2, 1, 1, ExtensionData.Empty);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolResultNormalizationInfo([ToolResultNormalizationTransformation.Redacted], 10, 2, 1, 1, ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
