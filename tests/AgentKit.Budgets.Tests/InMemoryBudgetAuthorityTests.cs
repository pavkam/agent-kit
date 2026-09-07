// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

public sealed class InMemoryBudgetAuthorityTests
{
    [Fact]
    public async Task CreateChildScopeAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var authority = TestFactory.Authority();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => authority.CreateChildScopeAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenNoParent_CreatesRootScope()
    {
        var authority = TestFactory.Authority();

        var result = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), TestContext.Current.CancellationToken);

        var created = result.ShouldBeOfType<BudgetScopeCreated>();
        created.Scope.ShouldBeOfType<InMemoryBudgetScope>().Depth.ShouldBe(0);
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenParentDoesNotExist_ReturnsParentNotFound()
    {
        var authority = TestFactory.Authority();

        var result = await authority.CreateChildScopeAsync(
            TestFactory.ScopeRequest(parentScopeId: new BudgetScopeId(Guid.NewGuid())), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<BudgetScopeCreationFailed>();
        failed.Kind.ShouldBe(BudgetScopeCreationFailureKind.ParentNotFound);
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenParentExists_CreatesChildWithIncrementedDepth()
    {
        var authority = TestFactory.Authority();
        var root = await TestFactory.CreateRootScopeAsync(authority);

        var result = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(parentScopeId: root.Id), TestContext.Current.CancellationToken);

        var created = result.ShouldBeOfType<BudgetScopeCreated>();
        created.Scope.ShouldBeOfType<InMemoryBudgetScope>().Depth.ShouldBe(1);
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenMaximumDepthReached_ReturnsMaximumDepthExceeded()
    {
        var authority = TestFactory.Authority(options: TestFactory.DefaultOptions(maximumScopeDepth: 1));
        var root = await TestFactory.CreateRootScopeAsync(authority);

        var result = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(parentScopeId: root.Id), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<BudgetScopeCreationFailed>();
        failed.Kind.ShouldBe(BudgetScopeCreationFailureKind.MaximumDepthExceeded);
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenDimensionIsUnregistered_ReturnsInvalidLimit()
    {
        var authority = TestFactory.Authority();
        var limit = TestFactory.HardLimit(new BudgetDimension("unregistered.dimension"), 10);

        var result = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(limits: [limit]), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<BudgetScopeCreationFailed>();
        failed.Kind.ShouldBe(BudgetScopeCreationFailureKind.InvalidLimit);
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenUnitIsNotAllowedForDimension_ReturnsInvalidLimit()
    {
        var authority = TestFactory.Authority();
        var limit = TestFactory.HardLimit(TestFactory.TestDimension, 10, new BudgetUnit("bytes"));

        var result = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(limits: [limit]), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<BudgetScopeCreationFailed>();
        failed.Kind.ShouldBe(BudgetScopeCreationFailureKind.InvalidLimit);
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenChildLimitIsWiderThanHardAncestorLimit_ReturnsLimitWiderThanAncestor()
    {
        var authority = TestFactory.Authority();
        var root = await TestFactory.CreateRootScopeAsync(
            authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);

        var result = await authority.CreateChildScopeAsync(
            TestFactory.ScopeRequest(parentScopeId: root.Id, limits: [TestFactory.HardLimit(TestFactory.TestDimension, 20)]), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<BudgetScopeCreationFailed>();
        failed.Kind.ShouldBe(BudgetScopeCreationFailureKind.LimitWiderThanAncestor);
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenChildLimitIsNarrowerThanHardAncestorLimit_Succeeds()
    {
        var authority = TestFactory.Authority();
        var root = await TestFactory.CreateRootScopeAsync(
            authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);

        var result = await authority.CreateChildScopeAsync(
            TestFactory.ScopeRequest(parentScopeId: root.Id, limits: [TestFactory.HardLimit(TestFactory.TestDimension, 5)]), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetScopeCreated>();
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenChildLimitIsWiderThanSoftAncestorLimit_Succeeds()
    {
        var authority = TestFactory.Authority();
        var root = await TestFactory.CreateRootScopeAsync(
            authority, [TestFactory.SoftLimit(TestFactory.TestDimension, 10)]);

        var result = await authority.CreateChildScopeAsync(
            TestFactory.ScopeRequest(parentScopeId: root.Id, limits: [TestFactory.HardLimit(TestFactory.TestDimension, 20)]), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetScopeCreated>();
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenCalledTwiceWithSameIdempotencyKey_ReturnsSameScope()
    {
        var authority = TestFactory.Authority();
        var request = TestFactory.ScopeRequest(idempotencyKey: "scope-key");

        var first = await authority.CreateChildScopeAsync(request, TestContext.Current.CancellationToken);
        var second = await authority.CreateChildScopeAsync(request, TestContext.Current.CancellationToken);

        var firstScope = first.ShouldBeOfType<BudgetScopeCreated>().Scope;
        var secondScope = second.ShouldBeOfType<BudgetScopeCreated>().Scope;
        secondScope.Id.ShouldBe(firstScope.Id);
    }

    [Fact]
    public void Constructor_WhenAnyDependencyIsNull_ThrowsArgumentNullException()
    {
        var dimensions = TestFactory.DefaultCatalog();
        var scopeIds = new GuidIdentifierGenerator<BudgetScopeId>(static v => new BudgetScopeId(v));
        var reservationIds = new GuidIdentifierGenerator<BudgetReservationId>(static v => new BudgetReservationId(v));
        var timeProvider = TimeProvider.System;
        var options = TestFactory.DefaultOptions();

        _ = Should.Throw<ArgumentNullException>(
            () => new InMemoryBudgetAuthority(null!, scopeIds, reservationIds, timeProvider, options));
        _ = Should.Throw<ArgumentNullException>(
            () => new InMemoryBudgetAuthority(dimensions, null!, reservationIds, timeProvider, options));
        _ = Should.Throw<ArgumentNullException>(
            () => new InMemoryBudgetAuthority(dimensions, scopeIds, null!, timeProvider, options));
        _ = Should.Throw<ArgumentNullException>(
            () => new InMemoryBudgetAuthority(dimensions, scopeIds, reservationIds, null!, options));
        _ = Should.Throw<ArgumentNullException>(
            () => new InMemoryBudgetAuthority(dimensions, scopeIds, reservationIds, timeProvider, null!));
    }
}
