// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryListResult"/> factories.</summary>
public sealed class MemoryListResultTests
{
    [Fact]
    public void Page_WhenValid_ReportsAPageWithWatermark()
    {
        var record = MemoryTestData.Record(MemoryTestData.NewOwner());

        var result = MemoryListResult.Page([record], 4, 9);

        result.IsPage.ShouldBeTrue();
        result.Items.ShouldBe([record]);
        result.NextCursor.ShouldBe(4);
        result.DeletionGeneration.ShouldBe(9);
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Page_WhenItemsAreDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => MemoryListResult.Page(default, null, 0)).ParamName.ShouldBe("items");

    [Fact]
    public void Page_WhenAnItemIsNull_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => MemoryListResult.Page([null!], null, 0)).ParamName.ShouldBe("items");

    [Fact]
    public void Page_WhenCursorOrGenerationIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => MemoryListResult.Page([], -1, 0)).ParamName.ShouldBe("nextCursor");
        Should.Throw<ArgumentOutOfRangeException>(() => MemoryListResult.Page([], null, -1)).ParamName.ShouldBe("deletionGeneration");
    }

    [Fact]
    public void Rejected_WhenFailureIsSupplied_ReportsNoPage()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.Denied, "no");

        var result = MemoryListResult.Rejected(failure);

        result.IsPage.ShouldBeFalse();
        result.Items.ShouldBeEmpty();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Rejected_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryListResult.Rejected(null!)).ParamName.ShouldBe("failure");
}
