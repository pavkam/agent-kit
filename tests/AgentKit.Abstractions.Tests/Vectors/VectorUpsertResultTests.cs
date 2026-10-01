// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

/// <summary>Verifies <see cref="VectorUpsertResult"/> factories.</summary>
public sealed class VectorUpsertResultTests
{
    [Fact]
    public void Succeeded_WhenValid_ReportsCountAndWatermark()
    {
        var result = VectorUpsertResult.Succeeded(3, 9, replayed: true);

        result.IsUpserted.ShouldBeTrue();
        result.Upserted.ShouldBe(3);
        result.Watermark.ShouldBe(9);
        result.Replayed.ShouldBeTrue();
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Succeeded_WhenACountIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => VectorUpsertResult.Succeeded(-1, 0, false)).ParamName.ShouldBe("upserted");
        Should.Throw<ArgumentOutOfRangeException>(() => VectorUpsertResult.Succeeded(0, -1, false)).ParamName.ShouldBe("watermark");
    }

    [Fact]
    public void Rejected_WhenFailureIsSupplied_ReportsNotUpserted()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.IncompatibleVectorSpace, "space");

        var result = VectorUpsertResult.Rejected(failure);

        result.IsUpserted.ShouldBeFalse();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Rejected_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => VectorUpsertResult.Rejected(null!)).ParamName.ShouldBe("failure");
}
