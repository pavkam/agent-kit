// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookDispatchContextTests
{
    [Fact]
    public void Constructor_WhenCatalogIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookDispatchContext(
            null!, HookKernelTestData.Dispatch(), new HookKernelTestData.FakeActivationLease()));

        exception.ParamName.ShouldBe("catalog");
    }

    [Fact]
    public void Constructor_WhenDispatchIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookDispatchContext(
            new HookCatalogSnapshot(HookKernelTestData.Profile, new HookCatalogVersion("v1"), []),
            null!,
            new HookKernelTestData.FakeActivationLease()));

        exception.ParamName.ShouldBe("dispatch");
    }

    [Fact]
    public void Constructor_WhenActivationIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookDispatchContext(
            new HookCatalogSnapshot(HookKernelTestData.Profile, new HookCatalogVersion("v1"), []),
            HookKernelTestData.Dispatch(),
            null!));

        exception.ParamName.ShouldBe("activation");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsEveryProperty()
    {
        var catalog = new HookCatalogSnapshot(HookKernelTestData.Profile, new HookCatalogVersion("v1"), []);
        var dispatch = HookKernelTestData.Dispatch();
        var activation = new HookKernelTestData.FakeActivationLease();

        var context = new HookDispatchContext(catalog, dispatch, activation);

        context.Catalog.ShouldBe(catalog);
        context.Dispatch.ShouldBe(dispatch);
        context.Activation.ShouldBe(activation);
    }

    [Fact]
    public void ForPoint_WhenCalled_KeepsCatalogActivationCorrelationAndDeadlineAndChangesPointIdentityAndStart()
    {
        var catalog = new HookCatalogSnapshot(HookKernelTestData.Profile, new HookCatalogVersion("v1"), []);
        var activation = new HookKernelTestData.FakeActivationLease();
        var context = new HookDispatchContext(catalog, HookKernelTestData.Dispatch(), activation);
        var id = new HookDispatchId(Guid.NewGuid());
        var start = context.Dispatch.Timestamp.AddSeconds(2);

        var derived = context.ForPoint(AgentHookPoints.BeforeMemoryWrite, id, start);

        derived.Catalog.ShouldBeSameAs(catalog);
        derived.Activation.ShouldBeSameAs(activation);
        derived.Dispatch.Point.ShouldBe(AgentHookPoints.BeforeMemoryWrite);
        derived.Dispatch.DispatchId.ShouldBe(id);
        derived.Dispatch.Correlation.ShouldBe(context.Dispatch.Correlation);
        derived.Dispatch.Timestamp.ShouldBe(start);
        derived.Dispatch.Deadline.ShouldBe(context.Dispatch.Deadline);
    }

    [Fact]
    public void ForPoint_WhenArgumentIsInvalidOrTheDeadlineHasPassed_ThrowsArgumentOutOfRangeException()
    {
        var context = new HookDispatchContext(
            new HookCatalogSnapshot(HookKernelTestData.Profile, new HookCatalogVersion("v1"), []),
            HookKernelTestData.Dispatch(),
            new HookKernelTestData.FakeActivationLease());
        var id = new HookDispatchId(Guid.NewGuid());

        Should.Throw<ArgumentOutOfRangeException>(() => context.ForPoint(default, id, context.Dispatch.Timestamp)).ParamName.ShouldBe("point");
        Should.Throw<ArgumentOutOfRangeException>(() => context.ForPoint(AgentHookPoints.BeforeMemoryWrite, default, context.Dispatch.Timestamp)).ParamName.ShouldBe("dispatchId");
        Should.Throw<ArgumentOutOfRangeException>(() => context.ForPoint(AgentHookPoints.BeforeMemoryWrite, id, context.Dispatch.Deadline)).ParamName.ShouldBe("deadline");
    }
}
