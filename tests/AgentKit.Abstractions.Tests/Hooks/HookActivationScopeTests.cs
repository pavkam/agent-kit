// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookActivationScopeTests
{
    [Fact]
    public void Constructor_WhenCatalogIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookActivationScope(
            null!, new HookKernelTestData.FakeActivationLease()));

        exception.ParamName.ShouldBe("catalog");
    }

    [Fact]
    public void Constructor_WhenActivationIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookActivationScope(
            new HookCatalogSnapshot(HookKernelTestData.Profile, new HookCatalogVersion("v1"), []), null!));

        exception.ParamName.ShouldBe("activation");
    }

    [Fact]
    public void CreateDispatch_WhenDispatchIsNull_ThrowsArgumentNullException()
    {
        var scope = new HookActivationScope(
            new HookCatalogSnapshot(HookKernelTestData.Profile, new HookCatalogVersion("v1"), []),
            new HookKernelTestData.FakeActivationLease());

        var exception = Should.Throw<ArgumentNullException>(() => scope.CreateDispatch(null!));
        exception.ParamName.ShouldBe("dispatch");
    }

    [Fact]
    public void CreateDispatch_WhenValid_SharesCatalogAndActivation()
    {
        var catalog = new HookCatalogSnapshot(HookKernelTestData.Profile, new HookCatalogVersion("v1"), []);
        var activation = new HookKernelTestData.FakeActivationLease();
        var scope = new HookActivationScope(catalog, activation);
        var dispatch = HookKernelTestData.Dispatch();

        var context = scope.CreateDispatch(dispatch);

        context.Catalog.ShouldBe(catalog);
        context.Dispatch.ShouldBe(dispatch);
        context.Activation.ShouldBe(activation);
    }

    [Fact]
    public async Task CreateDispatch_AfterDisposal_ThrowsObjectDisposedException()
    {
        var scope = new HookActivationScope(
            new HookCatalogSnapshot(HookKernelTestData.Profile, new HookCatalogVersion("v1"), []),
            new HookKernelTestData.FakeActivationLease());

        await scope.DisposeAsync();

        _ = Should.Throw<ObjectDisposedException>(() => scope.CreateDispatch(HookKernelTestData.Dispatch()));
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_DisposesActivationExactlyOnce()
    {
        var activation = new HookKernelTestData.FakeActivationLease();
        var scope = new HookActivationScope(
            new HookCatalogSnapshot(HookKernelTestData.Profile, new HookCatalogVersion("v1"), []), activation);

        await scope.DisposeAsync();
        await scope.DisposeAsync();

        activation.DisposeCount.ShouldBe(1);
    }
}
