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

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var rejection = RejectionPolicy();
        var projection = ProjectionPolicy();
        var bounds = new ToolResultBounds(1, 1);
        var version = new ToolResultNormalizationAlgorithmVersion(1);
        var snapshot = new ToolResultNormalizationSnapshot(rejection, projection, null, version, bounds, ToolResultProjectionTransformations.Redaction, ExtensionData.Empty);
        snapshot.RejectionPolicy.ShouldBe(rejection);
        snapshot.ProjectionPolicy.ShouldBe(projection);
        snapshot.AlgorithmVersion.ShouldBe(version);
        snapshot.Bounds.ShouldBe(bounds);
        snapshot.AllowedTransformations.ShouldBe(ToolResultProjectionTransformations.Redaction);
        snapshot.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolResultNormalizationSnapshot(RejectionPolicy(), ProjectionPolicy(), null, new ToolResultNormalizationAlgorithmVersion(1), new ToolResultBounds(1, 1), ToolResultProjectionTransformations.None, ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ToolResultProjectionPolicyReference ProjectionPolicy() => new(new ToolResultProjectionPolicyKey("projection"), new ToolResultProjectionPolicyVersion(1));
    private static ToolResultRejectionPolicyReference RejectionPolicy() => new(new ToolResultRejectionPolicyKey("rejection"), new ToolResultRejectionPolicyVersion(1));
}
