// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

public sealed class HookRegistrationCatalogTests
{
    [Fact]
    public async Task CaptureAsync_WhenNoHooks_ReturnsEmptySnapshot()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentHooks();
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IHookCatalog>();

        var snapshot = await catalog.CaptureAsync(
            new HookCatalogRequest(HookProfileOptions.DefaultProfileKey),
            TestContext.Current.CancellationToken);

        snapshot.ProfileKey.ShouldBe(HookProfileOptions.DefaultProfileKey);
        snapshot.Registrations.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task CaptureAsync_WhenRunStartedHookRegistered_CapturesDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddRunStartedHook<CatalogRunStartedHook>();
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IHookCatalog>();

        var snapshot = await catalog.CaptureAsync(
            new HookCatalogRequest(HookProfileOptions.DefaultProfileKey),
            TestContext.Current.CancellationToken);

        snapshot.Registrations.Length.ShouldBe(1);
        snapshot.Registrations[0].Point.ShouldBe(AgentHookPoints.RunStarted);
        snapshot.Registrations[0].Id.ShouldBe(HookRegistrationIds.FromAuthorHookId(new HookId("catalog.run-started")));
    }

    [Fact]
    public async Task CaptureAsync_WhenDuplicateRegistrationId_ThrowsHookCompositionException()
    {
        var services = new ServiceCollection();
        _ = services.AddRunStartedHook<DuplicateOneRunStartedHook>();
        _ = services.AddRunStartedHook<DuplicateTwoRunStartedHook>();
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IHookCatalog>();

        _ = await Should.ThrowAsync<HookCompositionException>(() => catalog.CaptureAsync(
            new HookCatalogRequest(HookProfileOptions.DefaultProfileKey),
            TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task CreateAsync_WhenCatalogCaptured_ResolvesRegisteredHook()
    {
        var services = new ServiceCollection();
        _ = services.AddRunStartedHook<CatalogRunStartedHook>();
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IHookCatalog>();
        var factory = provider.GetRequiredService<IHookInstanceFactory>();
        var snapshot = await catalog.CaptureAsync(
            new HookCatalogRequest(HookProfileOptions.DefaultProfileKey),
            TestContext.Current.CancellationToken);
        await using var lease = await factory.CreateAsync(snapshot, TestContext.Current.CancellationToken);

        var resolution = await lease.ResolveAsync<IRunStartedHook>(
            snapshot.Registrations[0].Id,
            TestContext.Current.CancellationToken);

        _ = resolution.ShouldBeOfType<HookInstanceResolved<IRunStartedHook>>().Hook.ShouldBeOfType<CatalogRunStartedHook>();
    }

    private sealed class CatalogRunStartedHook: IRunStartedHook
    {
        public HookId Id { get; } = new("catalog.run-started");

        public ValueTask OnRunStartedAsync(RunStartedEventArgs args, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class DuplicateOneRunStartedHook: IRunStartedHook
    {
        public HookId Id { get; } = new("duplicate");

        public ValueTask OnRunStartedAsync(RunStartedEventArgs args, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class DuplicateTwoRunStartedHook: IRunStartedHook
    {
        public HookId Id { get; } = new("duplicate");

        public ValueTask OnRunStartedAsync(RunStartedEventArgs args, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
