// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryListRequest"/> constraints and defaults.</summary>
public sealed class MemoryListRequestTests
{
    private static SecurityGrant Grant() => MemoryTestData.Grant(MemoryTestData.NewOwner());

    [Fact]
    public void Constructor_WhenStatesAndTermsAreDefault_ListsActiveRecordsWithoutATextFilter()
    {
        var request = new MemoryListRequest(null, default, default, 0, 10, Grant());

        request.States.ShouldBe([MemoryLifecycleState.Active]);
        request.Terms.ShouldBeEmpty();
        request.Namespace.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenFiltersAreSupplied_PreservesThem()
    {
        var request = new MemoryListRequest(new MemoryNamespace("n"), [MemoryLifecycleState.Accepted], ["a", "b"], 5, 7, Grant());

        request.Namespace.ShouldBe(new MemoryNamespace("n"));
        request.States.ShouldBe([MemoryLifecycleState.Accepted]);
        request.Terms.ShouldBe(["a", "b"]);
        request.AfterSequence.ShouldBe(5);
        request.Limit.ShouldBe(7);
    }

    [Fact]
    public void Constructor_WhenNamespaceIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryListRequest(default(MemoryNamespace), default, default, 0, 10, Grant())).ParamName.ShouldBe("namespace");

    [Fact]
    public void Constructor_WhenAStateIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryListRequest(null, [(MemoryLifecycleState) 99], default, 0, 10, Grant())).ParamName.ShouldBe("states");

    [Fact]
    public void Constructor_WhenATermIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new MemoryListRequest(null, default, ["ok", " "], 0, 10, Grant())).ParamName.ShouldBe("terms");

    [Fact]
    public void Constructor_WhenCursorIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryListRequest(null, default, default, -1, 10, Grant())).ParamName.ShouldBe("afterSequence");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(MemoryListRequest.MaximumLimit + 1)]
    public void Constructor_WhenLimitIsOutOfRange_ThrowsArgumentOutOfRangeException(int limit) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryListRequest(null, default, default, 0, limit, Grant())).ParamName.ShouldBe("limit");

    [Fact]
    public void Constructor_WhenLimitIsTheMaximum_Accepts() =>
        new MemoryListRequest(null, default, default, 0, MemoryListRequest.MaximumLimit, Grant()).Limit.ShouldBe(MemoryListRequest.MaximumLimit);

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryListRequest(null, default, default, 0, 10, null!)).ParamName.ShouldBe("grant");
}
