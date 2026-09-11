// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolResultNormalizationSnapshot behavior and contracts.</summary>
public sealed class ToolResultNormalizationSnapshotTests
{
    [Fact]
    public void ToolResultNormalizationSnapshot_Constructor_WhenFlagsUnknown_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultNormalizationSnapshot(RejectionPolicy(), ProjectionPolicy(), null, new ToolResultNormalizationAlgorithmVersion(1), new ToolResultBounds(1, 1), (ToolResultProjectionTransformations) (1 << 20), ExtensionData.Empty));
        exception.ParamName.ShouldBe("allowedTransformations");
    }

    [Fact]
    public void ToolResultNormalizationSnapshot_Constructor_WhenRequiredPolicyNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultNormalizationSnapshot(null!, ProjectionPolicy(), null, new ToolResultNormalizationAlgorithmVersion(1), new ToolResultBounds(1, 1), ToolResultProjectionTransformations.None, ExtensionData.Empty));
        exception.ParamName.ShouldBe("rejectionPolicy");
    }

    private static ToolResultProjectionPolicyReference ProjectionPolicy() => new(new ToolResultProjectionPolicyKey("projection"), new ToolResultProjectionPolicyVersion(1));
    private static ToolResultRejectionPolicyReference RejectionPolicy() => new(new ToolResultRejectionPolicyKey("rejection"), new ToolResultRejectionPolicyVersion(1));
}
