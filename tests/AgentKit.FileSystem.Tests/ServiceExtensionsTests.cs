// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

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
    public void AddSandboxedFileSystem_WhenCalled_RegistersFileSystemWithConfiguredRoot()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));

        _ = services.AddSandboxedFileSystem(root);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IFileSystem>().ShouldBeOfType<SandboxedFileSystem>();
        provider.GetRequiredService<IDirectoryReader>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IFileGlobber>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IFileContentSearcher>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IFileSnapshotReader>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IAtomicFileReplacer>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IWorkspacePatchApplier>().ShouldBeSameAs(provider.GetRequiredService<IFileSystem>());
        provider.GetRequiredService<IOptions<SandboxedFileSystemOptions>>().Value.RootDirectory.ShouldBe(root);
    }

    [Fact]
    public void AddSandboxedFileSystem_WhenCalledTwice_KeepsFirstServiceRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));

        _ = services.AddSandboxedFileSystem(root);
        _ = services.AddSandboxedFileSystem(root);
        using var provider = services.BuildServiceProvider();

        provider.GetServices<IFileSystem>().Count().ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddSandboxedFileSystem_WhenFileSystemIsReplaced_PreservesReplacementAndDefaultNarrowCapabilities(
        bool replaceAfterRegistration)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        var replacement = new ReplacementFileSystem();
        if (!replaceAfterRegistration)
        {
            _ = services.AddSingleton<IFileSystem>(replacement);
        }

        _ = services.AddSandboxedFileSystem(Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N")));
        if (replaceAfterRegistration)
        {
            _ = services.Replace(ServiceDescriptor.Singleton<IFileSystem>(replacement));
        }

        using var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<SandboxedFileSystem>();

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
    public void AddSandboxedFileSystem_WhenNarrowCapabilityIsReplaced_PreservesIndependentReplacement(
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

        _ = services.AddSandboxedFileSystem(Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N")));
        if (replaceAfterRegistration)
        {
            _ = services.Replace(ServiceDescriptor.Singleton(capabilityType, replacement));
        }

        using var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<SandboxedFileSystem>();

        provider.GetRequiredService(capabilityType).ShouldBeSameAs(replacement);
        _ = provider.GetServices(capabilityType).ShouldHaveSingleItem();
        _ = provider.GetRequiredService<IFileSystem>().ShouldBeOfType<SandboxedFileSystem>();
        foreach (var untouchedType in _narrowCapabilityTypes.Where(type => type != capabilityType))
        {
            provider.GetRequiredService(untouchedType).ShouldBeSameAs(concrete);
        }
    }

    [Fact]
    public void AddSandboxedFileSystem_WhenRootDirectoryBlank_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());

        _ = Should.Throw<ArgumentException>(() => services.AddSandboxedFileSystem("   "));
    }

    [Fact]
    public void AddSandboxedFileSystem_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));

        _ = services.AddSandboxedFileSystem(root, o => o.MaximumReadBytes = 123);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<SandboxedFileSystemOptions>>().Value.MaximumReadBytes.ShouldBe(123L);
    }

    [Fact]
    public void AddSandboxedFileSystem_WhenMaximumReadBytesIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));
        _ = services.AddSandboxedFileSystem(root, o => o.MaximumReadBytes = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<SandboxedFileSystemOptions>>().Value);
    }

    [Fact]
    public void AddSandboxedFileSystem_WhenSearchDurationIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));
        _ = services.AddSandboxedFileSystem(root, static options => options.MaximumSearchDuration = TimeSpan.Zero);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<SandboxedFileSystemOptions>>().Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddSandboxedFileSystem_WhenPatchBoundaryIsNotPositive_FailsValidationOnAccess(bool entryBoundary)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));
        _ = services.AddSandboxedFileSystem(root, options =>
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
            () => provider.GetRequiredService<IOptions<SandboxedFileSystemOptions>>().Value);
    }
}
