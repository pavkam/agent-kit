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
}
