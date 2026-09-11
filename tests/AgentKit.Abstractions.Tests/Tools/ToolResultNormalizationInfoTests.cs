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
}
