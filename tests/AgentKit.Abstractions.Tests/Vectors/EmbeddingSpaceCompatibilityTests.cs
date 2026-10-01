// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="EmbeddingSpaceCompatibility"/> decides comparable spaces by stable facts only.</summary>
public sealed class EmbeddingSpaceCompatibilityTests
{
    [Fact]
    public void IsSameVectorSpaceAs_WhenOnlyPerResponseIdentifiersDiffer_ReturnsTrue() =>
        MemoryTestData.EmbeddingSpace().IsSameVectorSpaceAs(MemoryTestData.EmbeddingSpace()).ShouldBeTrue();

    [Fact]
    public void IsSameVectorSpaceAs_WhenPurposeDiffers_ReturnsTrueBecauseQueryAndDocumentShareASpace() =>
        MemoryTestData.EmbeddingSpace(purpose: EmbeddingPurpose.Query).IsSameVectorSpaceAs(MemoryTestData.EmbeddingSpace(purpose: EmbeddingPurpose.Document)).ShouldBeTrue();

    [Fact]
    public void IsSameVectorSpaceAs_WhenDimensionsDiffer_ReturnsFalse() =>
        MemoryTestData.EmbeddingSpace(3).IsSameVectorSpaceAs(MemoryTestData.EmbeddingSpace(4)).ShouldBeFalse();

    [Fact]
    public void IsSameVectorSpaceAs_WhenModelDiffersButDimensionsMatch_ReturnsFalse() =>
        MemoryTestData.EmbeddingSpace(3, "embed-1").IsSameVectorSpaceAs(MemoryTestData.EmbeddingSpace(3, "embed-2")).ShouldBeFalse();

    [Fact]
    public void IsSameVectorSpaceAs_WhenNormalizationTruncationOrRevisionDiffers_ReturnsFalse()
    {
        var baseline = MemoryTestData.EmbeddingSpace();

        baseline.IsSameVectorSpaceAs(baseline with { Normalization = EmbeddingNormalization.Unit }).ShouldBeFalse();
        baseline.IsSameVectorSpaceAs(baseline with { Truncation = EmbeddingTruncation.Reject }).ShouldBeFalse();
        baseline.IsSameVectorSpaceAs(baseline with { ModelRevision = new ProviderModelRevision("r2") }).ShouldBeFalse();
    }

    [Fact]
    public void IsSameVectorSpaceAs_WhenElementTypeDiffers_ReturnsFalse()
    {
        var baseline = MemoryTestData.EmbeddingSpace();

        baseline.IsSameVectorSpaceAs(baseline with { ElementType = EmbeddingElementType.Int8 }).ShouldBeFalse();
    }

    [Fact]
    public void IsSameVectorSpaceAs_WhenOtherIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryTestData.EmbeddingSpace().IsSameVectorSpaceAs(null!)).ParamName.ShouldBe("other");
}
