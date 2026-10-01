// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using Microsoft.Extensions.Options;

/// <summary>Verifies CompactionProfileRegistry behavior and contracts.</summary>
public sealed class CompactionProfileRegistryTests
{
    private static readonly ComponentKey<ICompactor> _compactor = new("registry-compactor");
    private static readonly CompactionProfileKey _profile = new("registry-profile");

    [Fact]
    public void Build_WhenProviderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => CompactionProfileRegistry.Build(null!)).ParamName.ShouldBe("provider");

    [Fact]
    public void Build_WhenNoProfileIsRegistered_ReturnsAnEmptyCatalog()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var registry = CompactionProfileRegistry.Build(provider);

        registry.TryGet(_profile, out var publication).ShouldBeFalse();
        publication.ShouldBeNull();
    }

    [Fact]
    public void TryGet_WhenKeyIsDefault_ThrowsArgumentOutOfRangeException()
    {
        using var provider = Provider(static _ => { });
        var registry = CompactionProfileRegistry.Build(provider);

        Should.Throw<ArgumentOutOfRangeException>(() => registry.TryGet(default, out _)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void TryGet_WhenProfileIsNotRegistered_ReturnsFalse()
    {
        using var provider = Provider(static _ => { });
        var registry = CompactionProfileRegistry.Build(provider);

        registry.TryGet(new CompactionProfileKey("unknown"), out var publication).ShouldBeFalse();
        publication.ShouldBeNull();
    }

    [Fact]
    public void TryGet_WhenProfileIsRegistered_ReturnsItsPublication()
    {
        using var provider = Provider(static _ => { });
        var registry = CompactionProfileRegistry.Build(provider);

        registry.TryGet(_profile, out var publication).ShouldBeTrue();

        publication.ShouldNotBeNull().ProfileKey.ShouldBe(_profile);
        publication.CompactorKey.ShouldBe(_compactor);
    }

    [Fact]
    public void Build_WhenTheSameConfigurationIsCompiledTwice_ProducesTheSameFingerprint()
    {
        using var first = Provider(static _ => { });
        using var second = Provider(static _ => { });

        _ = CompactionProfileRegistry.Build(first).TryGet(_profile, out var one);
        _ = CompactionProfileRegistry.Build(second).TryGet(_profile, out var two);

        one.ShouldNotBeNull().Policy.ConfigurationFingerprint.ShouldBe(two.ShouldNotBeNull().Policy.ConfigurationFingerprint);
    }

    [Fact]
    public void Build_WhenACeilingChanges_ChangesTheFingerprint()
    {
        using var first = Provider(static _ => { });
        using var second = Provider(static services =>
            services.Configure<ContextCompactionOptions>(_compactor.Value, options => options.MaximumAttempts = 9));

        _ = CompactionProfileRegistry.Build(first).TryGet(_profile, out var one);
        _ = CompactionProfileRegistry.Build(second).TryGet(_profile, out var two);

        one.ShouldNotBeNull().Policy.ConfigurationFingerprint.ShouldNotBe(two.ShouldNotBeNull().Policy.ConfigurationFingerprint);
    }

    [Fact]
    public void Build_WhenCompactorOptionsAreInvalid_ThrowsOptionsValidationException()
    {
        using var provider = Provider(static services =>
            services.Configure<ContextCompactionOptions>(_compactor.Value, options => options.MaximumAttempts = 0));

        _ = Should.Throw<OptionsValidationException>(() => CompactionProfileRegistry.Build(provider));
    }

    private static ServiceProvider Provider(Action<IServiceCollection> customize)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISessionCoordinator>(new FakeSessionCoordinator(new BranchId(Guid.NewGuid())));
        _ = services.AddAgentContextCompaction(_compactor);
        _ = services.AddCompactionProfile(_profile, _compactor, _ => { });
        customize(services);
        return services.BuildServiceProvider();
    }
}
