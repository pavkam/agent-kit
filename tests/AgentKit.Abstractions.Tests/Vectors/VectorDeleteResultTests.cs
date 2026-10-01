// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

/// <summary>Verifies <see cref="VectorDeleteResult"/> factories.</summary>
public sealed class VectorDeleteResultTests
{
    [Fact]
    public void Succeeded_WhenValid_ReportsCountAndWatermark()
    {
        var result = VectorDeleteResult.Succeeded(2, 7);

        result.IsDeleted.ShouldBeTrue();
        result.Deleted.ShouldBe(2);
        result.Watermark.ShouldBe(7);
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Succeeded_WhenACountIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => VectorDeleteResult.Succeeded(-1, 0)).ParamName.ShouldBe("deleted");
        Should.Throw<ArgumentOutOfRangeException>(() => VectorDeleteResult.Succeeded(0, -1)).ParamName.ShouldBe("watermark");
    }

    [Fact]
    public void Rejected_WhenFailureIsSupplied_ReportsNotDeleted()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.Denied, "no");

        var result = VectorDeleteResult.Rejected(failure);

        result.IsDeleted.ShouldBeFalse();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Rejected_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => VectorDeleteResult.Rejected(null!)).ParamName.ShouldBe("failure");
}
