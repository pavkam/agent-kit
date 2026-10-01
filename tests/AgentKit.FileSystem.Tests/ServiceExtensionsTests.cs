// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Verifies the keyed operating-system file-system registration surface.</summary>
public sealed class ServiceExtensionsTests
{
    private static readonly FileSystemProfileKey _key = new("workspace");

    private static readonly Type[] _hostCapabilityTypes =
    [
        typeof(IDirectoryReader),
        typeof(IFileGlobber),
        typeof(IFileContentSearcher),
        typeof(IFileSnapshotReader),
        typeof(IAtomicFileReplacer),
        typeof(IWorkspacePatchApplier),
    ];

    /// <summary>Gets every host capability a host may replace.</summary>
    public static TheoryData<Type> HostCapabilityReplacementCases { get; } =
    [
        typeof(IDirectoryReader),
        typeof(IFileGlobber),
        typeof(IFileContentSearcher),
        typeof(IFileSnapshotReader),
        typeof(IAtomicFileReplacer),
        typeof(IWorkspacePatchApplier),
    ];

    /// <summary>Verifies one profile exposes every capability, and the workspace capabilities share one host instance.</summary>
    [Fact]
    public void AddOperatingSystemFileSystem_WhenCalled_RegistersEveryKeyedCapabilityOverOneWorkspaceHost()
    {
        using var provider = BuildProvider(out _);

        var host = provider.GetRequiredKeyedService<OperatingSystemWorkspaceHost>(_key.Value);
        _ = provider.GetRequiredKeyedService<IFileReader>(_key.Value).ShouldBeOfType<OperatingSystemFileReader>();
        _ = provider.GetRequiredKeyedService<IFileWriter>(_key.Value).ShouldBeOfType<OperatingSystemFileWriter>();
        foreach (var type in _hostCapabilityTypes)
        {
            provider.GetRequiredKeyedService(type, _key.Value).ShouldBeSameAs(host);
        }
    }

    /// <summary>Verifies the workspace host shares the reader's and writer's profile-scoped audience.</summary>
    [Fact]
    public void AddOperatingSystemFileSystem_WhenCalled_ScopesEveryAudienceToTheProfileKey()
    {
        using var provider = BuildProvider(out _);

        var audience = provider.GetRequiredKeyedService<IFileReader>(_key.Value).SecurityAudience;
        audience.ShouldBe(new ComponentId("agentkit.filesystem.os.workspace"));
        provider.GetRequiredKeyedService<IFileWriter>(_key.Value).SecurityAudience.ShouldBe(audience);
        provider.GetRequiredKeyedService<IDirectoryReader>(_key.Value).SecurityAudience.ShouldBe(audience);
        provider.GetRequiredKeyedService<IFileGlobber>(_key.Value).SecurityAudience.ShouldBe(audience);
    }

    /// <summary>Verifies the selector discovers read, write, metadata, and enumerate capability for the profile.</summary>
    [Fact]
    public async Task AddOperatingSystemFileSystem_WhenSelectorResolvesProfile_SelectsTheKeyedReaderAndDirectoryReader()
    {
        using var provider = BuildProvider(out _);
        var selector = provider.GetRequiredService<IFileSystemSelector>();

        var reader = await selector.SelectAsync(_key, FileSystemCapability.Read, TestContext.Current.CancellationToken);
        var directory = await selector.SelectAsync(_key, FileSystemCapability.Enumerate, TestContext.Current.CancellationToken);
        var missing = await selector.SelectAsync(new FileSystemProfileKey("absent"), FileSystemCapability.Read, TestContext.Current.CancellationToken);

        reader.ShouldBeOfType<FileSystemReaderSelected>().Reader
            .ShouldBeSameAs(provider.GetRequiredKeyedService<IFileReader>(_key.Value));
        directory.ShouldBeOfType<FileSystemDirectoryReaderSelected>().DirectoryReader
            .ShouldBeSameAs(provider.GetRequiredKeyedService<IDirectoryReader>(_key.Value));
        _ = missing.ShouldBeOfType<FileSystemProfileMissing>();
    }

    /// <summary>Verifies every profile gets its own host rooted at its own directory.</summary>
    [Fact]
    public async Task AddOperatingSystemFileSystem_WhenTwoProfilesAreRegistered_EnumerateTheirOwnRoots()
    {
        var firstRoot = CreateRoot();
        var secondRoot = CreateRoot();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(firstRoot, "first.txt"), "1", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(secondRoot, "second.txt"), "2", TestContext.Current.CancellationToken);
            var services = new ServiceCollection();
            _ = services.AddSingleton(TestSecurity.GrantStore());
            _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());
            _ = services.AddOperatingSystemFileSystem(new FileSystemProfileKey("one"), o => o.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), firstRoot)));
            _ = services.AddOperatingSystemFileSystem(new FileSystemProfileKey("two"), o => o.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), secondRoot)));
            using var provider = services.BuildServiceProvider();

            var first = await EnumerateAsync(provider.GetRequiredKeyedService<IDirectoryReader>("one"));
            var second = await EnumerateAsync(provider.GetRequiredKeyedService<IDirectoryReader>("two"));

            first.ShouldBe(["first.txt"]);
            second.ShouldBe(["second.txt"]);
        }
        finally
        {
            Directory.Delete(firstRoot, recursive: true);
            Directory.Delete(secondRoot, recursive: true);
        }
    }

    /// <summary>Verifies a host-supplied intent generator reaches the workspace host's enforcement.</summary>
    [Fact]
    public async Task AddOperatingSystemFileSystem_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var root = CreateRoot();
        try
        {
            var expectedId = new SecurityEnforcementIntentId(Guid.Parse("82000000-0000-0000-0000-000000000008"));
            var store = new TestSecurity.RecordingGrantStore();
            var services = new ServiceCollection();
            _ = services.AddSingleton<ISecurityGrantStore>(store);
            _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());
            _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(
                new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
            _ = services.AddOperatingSystemFileSystem(_key, o => o.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), root)));
            using var provider = services.BuildServiceProvider();

            _ = await EnumerateAsync(provider.GetRequiredKeyedService<IDirectoryReader>(_key.Value));

            store.LastIntent.ShouldNotBeNull().Id.ShouldBe(expectedId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Verifies a host replaces one keyed capability after registration without disturbing the others.</summary>
    /// <param name="capabilityType">The capability to replace.</param>
    [Theory]
    [MemberData(nameof(HostCapabilityReplacementCases))]
    public void AddOperatingSystemFileSystem_WhenCapabilityIsReplacedAfterRegistration_PreservesIndependentReplacement(Type capabilityType)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());
        var replacement = new ReplacementFileCapabilities();
        _ = services.AddOperatingSystemFileSystem(
            _key,
            o => o.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), CreateRoot())));
        _ = services.Replace(ServiceDescriptor.KeyedSingleton(capabilityType, _key.Value, replacement));

        using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredKeyedService<OperatingSystemWorkspaceHost>(_key.Value);

        provider.GetRequiredKeyedService(capabilityType, _key.Value).ShouldBeSameAs(replacement);
        foreach (var untouchedType in _hostCapabilityTypes.Where(type => type != capabilityType))
        {
            provider.GetRequiredKeyedService(untouchedType, _key.Value).ShouldBeSameAs(host);
        }
    }

    /// <summary>Verifies configured workspace bounds reach the host.</summary>
    [Fact]
    public async Task AddOperatingSystemFileSystem_WhenWorkspaceBoundsAreConfigured_AppliesThemToEnumeration()
    {
        var root = CreateRoot();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "a", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "b.txt"), "b", TestContext.Current.CancellationToken);
            var services = new ServiceCollection();
            _ = services.AddSingleton(TestSecurity.GrantStore());
            _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());
            _ = services.AddOperatingSystemFileSystem(_key, o =>
            {
                o.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), root));
                o.WorkspaceBounds = FileSystemWorkspaceBounds.Default with { MaximumDirectorySnapshotEntries = 1 };
            });
            using var provider = services.BuildServiceProvider();

            _ = await Should.ThrowAsync<IOException>(
                async () => await EnumerateAsync(provider.GetRequiredKeyedService<IDirectoryReader>(_key.Value)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Verifies a profile without a root is rejected at composition.</summary>
    [Fact]
    public void AddOperatingSystemFileSystem_WhenNoRootIsRegistered_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        _ = Should.Throw<InvalidOperationException>(() => services.AddOperatingSystemFileSystem(_key, static _ => { }));
    }

    /// <summary>Verifies a null configure delegate is rejected before any registration.</summary>
    [Fact]
    public void AddOperatingSystemFileSystem_WhenConfigureIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentNullException>(() => services.AddOperatingSystemFileSystem(_key, null!));

        exception.ParamName.ShouldBe("configure");
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies a null service collection is rejected.</summary>
    [Fact]
    public void AddOperatingSystemFileSystem_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddOperatingSystemFileSystem(_key, static _ => { }));

        exception.ParamName.ShouldBe("services");
    }

    private static ServiceProvider BuildProvider(out string root)
    {
        root = CreateRoot();
        var registeredRoot = root;
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());
        _ = services.AddOperatingSystemFileSystem(
            _key,
            o => o.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), registeredRoot)));
        return services.BuildServiceProvider();
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<string[]> EnumerateAsync(IDirectoryReader reader)
    {
        var names = new List<string>();
        var target = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath("."),
            ".",
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            FileSecurityBinding.ContentFingerprint("target"u8));
        await foreach (var entry in reader.EnumerateAsync(
            new AuthorizedDirectoryEnumeration(target, TestSecurity.Grant()),
            TestContext.Current.CancellationToken))
        {
            names.Add(entry.Name.Value);
        }

        return [.. names];
    }

    private sealed class AcceptingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }
}
