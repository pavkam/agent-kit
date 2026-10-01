// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="VectorSpaceDescriptor"/> constraints and compatibility.</summary>
public sealed class VectorSpaceDescriptorTests
{
    [Fact]
    public void Constructor_WhenValid_ExposesDimensionsFromTheEmbeddingSpace()
    {
        var space = MemoryTestData.Space("idx", 8, VectorDistanceMetric.DotProduct);

        space.IndexKey.ShouldBe(new VectorIndexKey("idx"));
        space.Dimensions.ShouldBe(8);
        space.DistanceMetric.ShouldBe(VectorDistanceMetric.DotProduct);
    }

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new VectorSpaceDescriptor(default, MemoryTestData.EmbeddingSpace(), VectorDistanceMetric.Cosine)).ParamName.ShouldBe("indexKey");

    [Fact]
    public void Constructor_WhenEmbeddingSpaceIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new VectorSpaceDescriptor(new VectorIndexKey("k"), null!, VectorDistanceMetric.Cosine)).ParamName.ShouldBe("embeddingSpace");

    [Fact]
    public void Constructor_WhenMetricIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new VectorSpaceDescriptor(new VectorIndexKey("k"), MemoryTestData.EmbeddingSpace(), (VectorDistanceMetric) 9)).ParamName.ShouldBe("distanceMetric");

    [Fact]
    public void IsCompatibleWith_WhenAllPartsMatch_ReturnsTrue() =>
        MemoryTestData.Space().IsCompatibleWith(MemoryTestData.Space()).ShouldBeTrue();

    [Fact]
    public void IsCompatibleWith_WhenAnyPartDiffers_ReturnsFalse()
    {
        var baseline = MemoryTestData.Space();

        baseline.IsCompatibleWith(MemoryTestData.Space("other")).ShouldBeFalse();
        baseline.IsCompatibleWith(MemoryTestData.Space(metric: VectorDistanceMetric.Euclidean)).ShouldBeFalse();
        baseline.IsCompatibleWith(MemoryTestData.Space(dimensions: 4)).ShouldBeFalse();
        baseline.IsCompatibleWith(MemoryTestData.Space(model: "embed-2")).ShouldBeFalse();
    }

    [Fact]
    public void IsCompatibleWith_WhenOtherIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryTestData.Space().IsCompatibleWith(null!)).ParamName.ShouldBe("other");
}
