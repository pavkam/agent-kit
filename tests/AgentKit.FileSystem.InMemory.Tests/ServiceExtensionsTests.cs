// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory.Tests;

using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class ServiceExtensionsTests
{
    private static readonly Type[] _narrowCapabilityTypes =
    [
        typeof(IDirectoryReader),
        typeof(IFileGlobber),
        typeof(IFileContentSearcher),
        typeof(IFileSnapshotReader),
        typeof(IAtomicFileReplacer),
        typeof(IWorkspacePatchApplier),
    ];

    /// <summary>Gets every narrow capability in both registration orders.</summary>
    public static TheoryData<Type, bool> NarrowCapabilityReplacementCases { get; } = new()
    {
        { typeof(IDirectoryReader), false },
        { typeof(IFileGlobber), false },
        { typeof(IFileContentSearcher), false },
        { typeof(IFileSnapshotReader), false },
        { typeof(IAtomicFileReplacer), false },
        { typeof(IWorkspacePatchApplier), false },
        { typeof(IDirectoryReader), true },
        { typeof(IFileGlobber), true },
        { typeof(IFileContentSearcher), true },
        { typeof(IFileSnapshotReader), true },
        { typeof(IAtomicFileReplacer), true },
        { typeof(IWorkspacePatchApplier), true },
    };

    [Fact]
    public void AddInMemoryFileSystem_WhenCalled_RegistersEveryCapability()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());

        _ = services.AddInMemoryFileSystem();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IFileSystem>().ShouldBeOfType<InMemoryFileSystem>();
        provider.GetRequiredService<IDirectoryReader>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IFileGlobber>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IFileContentSearcher>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IFileSnapshotReader>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IAtomicFileReplacer>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IWorkspacePatchApplier>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
    }

    [Fact]
    public void AddInMemoryFileSystem_WhenCalledTwice_KeepsFirstServiceRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());

        _ = services.AddInMemoryFileSystem();
        _ = services.AddInMemoryFileSystem();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<IFileSystem>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task AddInMemoryFileSystem_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("82000000-0000-0000-0000-000000000008"));
        var store = new TestSecurity.RecordingGrantStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(
            new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        _ = services.AddInMemoryFileSystem();
        using var provider = services.BuildServiceProvider();
        var fileSystem = (InMemoryFileSystem) provider.GetRequiredService<IFileSystem>();
        fileSystem.Seed(new FileSystemPath("source.txt"), "source");

        _ = await provider.GetRequiredService<IFileSystem>().ReadAsync(
            new LegacyFileReadRequest(new FileSystemPath("source.txt"), TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        store.LastIntent.ShouldNotBeNull().Id.ShouldBe(expectedId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddInMemoryFileSystem_WhenFileSystemIsReplaced_PreservesReplacementAndDefaultNarrowCapabilities(
        bool replaceAfterRegistration)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        var replacement = new ReplacementFileSystem();
        if (!replaceAfterRegistration)
        {
            _ = services.AddSingleton<IFileSystem>(replacement);
        }

        _ = services.AddInMemoryFileSystem();
        if (replaceAfterRegistration)
        {
            _ = services.Replace(ServiceDescriptor.Singleton<IFileSystem>(replacement));
        }

        using var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<InMemoryFileSystem>();

        provider.GetRequiredService<IFileSystem>().ShouldBeSameAs(replacement);
        _ = provider.GetServices<IFileSystem>().ShouldHaveSingleItem();
        provider.GetRequiredService<IDirectoryReader>().ShouldBeSameAs(concrete);
        provider.GetRequiredService<IFileGlobber>().ShouldBeSameAs(concrete);
        provider.GetRequiredService<IFileContentSearcher>().ShouldBeSameAs(concrete);
        provider.GetRequiredService<IFileSnapshotReader>().ShouldBeSameAs(concrete);
        provider.GetRequiredService<IAtomicFileReplacer>().ShouldBeSameAs(concrete);
        provider.GetRequiredService<IWorkspacePatchApplier>().ShouldBeSameAs(concrete);
    }

    [Theory]
    [MemberData(nameof(NarrowCapabilityReplacementCases))]
    public void AddInMemoryFileSystem_WhenNarrowCapabilityIsReplaced_PreservesIndependentReplacement(
        Type capabilityType,
        bool replaceAfterRegistration)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        var replacement = new ReplacementFileCapabilities();
        if (!replaceAfterRegistration)
        {
            _ = services.AddSingleton(capabilityType, replacement);
        }

        _ = services.AddInMemoryFileSystem();
        if (replaceAfterRegistration)
        {
            _ = services.Replace(ServiceDescriptor.Singleton(capabilityType, replacement));
        }

        using var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<InMemoryFileSystem>();

        provider.GetRequiredService(capabilityType).ShouldBeSameAs(replacement);
        _ = provider.GetServices(capabilityType).ShouldHaveSingleItem();
        _ = provider.GetRequiredService<IFileSystem>().ShouldBeOfType<InMemoryFileSystem>();
        foreach (var untouchedType in _narrowCapabilityTypes.Where(type => type != capabilityType))
        {
            provider.GetRequiredService(untouchedType).ShouldBeSameAs(concrete);
        }
    }

    [Fact]
    public void AddInMemoryFileSystem_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());

        _ = services.AddInMemoryFileSystem(o => o.MaximumReadBytes = 123);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<InMemoryFileSystemOptions>>().Value.MaximumReadBytes.ShouldBe(123L);
    }

    [Fact]
    public void AddInMemoryFileSystem_WhenMaximumReadBytesIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        _ = services.AddInMemoryFileSystem(o => o.MaximumReadBytes = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<InMemoryFileSystemOptions>>().Value);
    }

    [Fact]
    public void AddInMemoryFileSystem_WhenSearchDurationIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        _ = services.AddInMemoryFileSystem(static options => options.MaximumSearchDuration = TimeSpan.Zero);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<InMemoryFileSystemOptions>>().Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddInMemoryFileSystem_WhenPatchBoundaryIsNotPositive_FailsValidationOnAccess(bool entryBoundary)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        _ = services.AddInMemoryFileSystem(options =>
        {
            if (entryBoundary)
            {
                options.MaximumPatchEntries = 0;
            }
            else
            {
                options.MaximumPatchBytes = 0;
            }
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<InMemoryFileSystemOptions>>().Value);
    }
}
