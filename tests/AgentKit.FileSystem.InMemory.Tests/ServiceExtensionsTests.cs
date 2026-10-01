// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory.Tests;

using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Verifies the keyed in-memory file-system registration surface.</summary>
public sealed class ServiceExtensionsTests
{
    private static readonly FileSystemProfileKey _key = new("volume");

    private static readonly Type[] _hostCapabilityTypes =
    [
        typeof(IFileReader),
        typeof(IFileWriter),
        typeof(IFileMetadataReader),
        typeof(IDirectoryCreator),
        typeof(IDirectoryReader),
        typeof(IFileGlobber),
        typeof(IFileContentSearcher),
        typeof(IFileSnapshotReader),
        typeof(IAtomicFileReplacer),
        typeof(IWorkspacePatchApplier),
    ];

    /// <summary>Gets every capability a host may replace.</summary>
    public static TheoryData<Type> CapabilityReplacementCases { get; } =
    [
        typeof(IFileReader),
        typeof(IFileWriter),
        typeof(IFileMetadataReader),
        typeof(IDirectoryCreator),
        typeof(IDirectoryReader),
        typeof(IFileGlobber),
        typeof(IFileContentSearcher),
        typeof(IFileSnapshotReader),
        typeof(IAtomicFileReplacer),
        typeof(IWorkspacePatchApplier),
    ];

    /// <summary>Verifies one key exposes every capability over one shared volume.</summary>
    [Fact]
    public void AddInMemoryFileSystem_WhenCalled_RegistersEveryKeyedCapabilityOverOneVolume()
    {
        using var provider = BuildProvider();

        var volume = provider.GetRequiredKeyedService<InMemoryFileSystem>(_key.Value);
        foreach (var type in _hostCapabilityTypes)
        {
            provider.GetRequiredKeyedService(type, _key.Value).ShouldBeSameAs(volume);
        }
    }

    /// <summary>Verifies the volume's audience is scoped to its profile key.</summary>
    [Fact]
    public void AddInMemoryFileSystem_WhenCalled_ScopesTheAudienceToTheProfileKey()
    {
        using var provider = BuildProvider();

        provider.GetRequiredKeyedService<IFileReader>(_key.Value).SecurityAudience
            .ShouldBe(new ComponentId("agentkit.filesystem.inmemory.volume"));
    }

    /// <summary>Verifies the selector discovers the profile's read and enumerate capabilities.</summary>
    [Fact]
    public async Task AddInMemoryFileSystem_WhenSelectorResolvesProfile_SelectsTheKeyedCapabilities()
    {
        using var provider = BuildProvider();
        var selector = provider.GetRequiredService<IFileSystemSelector>();

        var reader = await selector.SelectAsync(_key, FileSystemCapability.Read, TestContext.Current.CancellationToken);
        var directory = await selector.SelectAsync(_key, FileSystemCapability.Enumerate, TestContext.Current.CancellationToken);

        reader.ShouldBeOfType<FileSystemReaderSelected>().Reader
            .ShouldBeSameAs(provider.GetRequiredKeyedService<InMemoryFileSystem>(_key.Value));
        directory.ShouldBeOfType<FileSystemDirectoryReaderSelected>().DirectoryReader
            .ShouldBeSameAs(provider.GetRequiredKeyedService<InMemoryFileSystem>(_key.Value));
    }

    /// <summary>Verifies two keys hold independent trees.</summary>
    [Fact]
    public void AddInMemoryFileSystem_WhenTwoKeysAreRegistered_HoldIndependentVolumes()
    {
        var services = new ServiceCollection();
        AddDependencies(services);
        _ = services.AddInMemoryFileSystem(new FileSystemProfileKey("one"));
        _ = services.AddInMemoryFileSystem(new FileSystemProfileKey("two"));
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredKeyedService<InMemoryFileSystem>("one");
        var second = provider.GetRequiredKeyedService<InMemoryFileSystem>("two");

        first.ShouldNotBeSameAs(second);
        first.Seed(new FileSystemPath("only-in-one.txt"), "x");
        second.TryReadAllBytes(new FileSystemPath("only-in-one.txt"), out _).ShouldBeFalse();
    }

    /// <summary>Verifies a host-supplied intent generator reaches the volume's enforcement.</summary>
    [Fact]
    public async Task AddInMemoryFileSystem_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("82000000-0000-0000-0000-000000000008"));
        var store = new TestSecurity.RecordingGrantStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(
            new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        _ = services.AddInMemoryFileSystem(_key);
        using var provider = services.BuildServiceProvider();
        var volume = provider.GetRequiredKeyedService<InMemoryFileSystem>(_key.Value);
        volume.Seed(new FileSystemPath("source.txt"), "source");

        var target = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath("."),
            ".",
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            FileSecurityBinding.ContentFingerprint("target"u8));
        await foreach (var entry in volume.EnumerateAsync(
            new AuthorizedDirectoryEnumeration(target, TestSecurity.Grant()),
            TestContext.Current.CancellationToken))
        {
            entry.Name.Value.ShouldBe("source.txt");
        }

        store.LastIntent.ShouldNotBeNull().Id.ShouldBe(expectedId);
    }

    /// <summary>Verifies a host replaces one keyed capability after registration without disturbing the others.</summary>
    /// <param name="capabilityType">The capability to replace.</param>
    [Theory]
    [MemberData(nameof(CapabilityReplacementCases))]
    public void AddInMemoryFileSystem_WhenCapabilityIsReplacedAfterRegistration_PreservesIndependentReplacement(Type capabilityType)
    {
        var services = new ServiceCollection();
        AddDependencies(services);
        _ = services.AddInMemoryFileSystem(_key);
        var replacement = new ReplacementFileCapabilities();
        _ = services.Replace(ServiceDescriptor.KeyedSingleton(capabilityType, _key.Value, replacement));
        using var provider = services.BuildServiceProvider();
        var volume = provider.GetRequiredKeyedService<InMemoryFileSystem>(_key.Value);

        provider.GetRequiredKeyedService(capabilityType, _key.Value).ShouldBeSameAs(replacement);
        foreach (var untouchedType in _hostCapabilityTypes.Where(type => type != capabilityType))
        {
            provider.GetRequiredKeyedService(untouchedType, _key.Value).ShouldBeSameAs(volume);
        }
    }

    /// <summary>Verifies configured options are named by the profile key.</summary>
    [Fact]
    public void AddInMemoryFileSystem_WhenConfigureProvided_AppliesOptionsToThatKeyOnly()
    {
        var services = new ServiceCollection();
        AddDependencies(services);
        _ = services.AddInMemoryFileSystem(new FileSystemProfileKey("tuned"), o => o.MaximumReadBytes = 123);
        _ = services.AddInMemoryFileSystem(new FileSystemProfileKey("default"));
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<InMemoryFileSystemOptions>>();

        options.Get("tuned").MaximumReadBytes.ShouldBe(123L);
        options.Get("default").MaximumReadBytes.ShouldBe(10L * 1024 * 1024);
    }

    /// <summary>Verifies an invalid ceiling fails validation when the options are read.</summary>
    /// <param name="property">The option whose ceiling is set to zero.</param>
    [Theory]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumReadBytes))]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumWriteBytes))]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumDirectorySnapshotEntries))]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumSearchDepth))]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumSearchFiles))]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumSearchBytes))]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumSearchMatches))]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumSearchLineBytes))]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumSearchDuration))]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumPatchEntries))]
    [InlineData(nameof(InMemoryFileSystemOptions.MaximumPatchBytes))]
    public void AddInMemoryFileSystem_WhenACeilingIsNotPositive_FailsValidationOnAccess(string property)
    {
        var services = new ServiceCollection();
        AddDependencies(services);
        _ = services.AddInMemoryFileSystem(_key, options => Invalidate(options, property));
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptionsMonitor<InMemoryFileSystemOptions>>().Get(_key.Value));
    }

    /// <summary>Verifies a null service collection is rejected.</summary>
    [Fact]
    public void AddInMemoryFileSystem_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddInMemoryFileSystem(_key)).ParamName.ShouldBe("services");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        AddDependencies(services);
        _ = services.AddInMemoryFileSystem(_key);
        return services.BuildServiceProvider();
    }

    private static void Invalidate(InMemoryFileSystemOptions options, string property)
    {
        switch (property)
        {
            case nameof(InMemoryFileSystemOptions.MaximumReadBytes): options.MaximumReadBytes = 0; break;
            case nameof(InMemoryFileSystemOptions.MaximumWriteBytes): options.MaximumWriteBytes = 0; break;
            case nameof(InMemoryFileSystemOptions.MaximumDirectorySnapshotEntries): options.MaximumDirectorySnapshotEntries = 0; break;
            case nameof(InMemoryFileSystemOptions.MaximumSearchDepth): options.MaximumSearchDepth = 0; break;
            case nameof(InMemoryFileSystemOptions.MaximumSearchFiles): options.MaximumSearchFiles = 0; break;
            case nameof(InMemoryFileSystemOptions.MaximumSearchBytes): options.MaximumSearchBytes = 0; break;
            case nameof(InMemoryFileSystemOptions.MaximumSearchMatches): options.MaximumSearchMatches = 0; break;
            case nameof(InMemoryFileSystemOptions.MaximumSearchLineBytes): options.MaximumSearchLineBytes = 0; break;
            case nameof(InMemoryFileSystemOptions.MaximumSearchDuration): options.MaximumSearchDuration = TimeSpan.Zero; break;
            case nameof(InMemoryFileSystemOptions.MaximumPatchEntries): options.MaximumPatchEntries = 0; break;
            case nameof(InMemoryFileSystemOptions.MaximumPatchBytes): options.MaximumPatchBytes = 0; break;
            default: throw new ArgumentOutOfRangeException(nameof(property), property, "Unknown option.");
        }
    }

    private static void AddDependencies(IServiceCollection services)
    {
        _ = services.AddSingleton(TestSecurity.GrantStore());
        _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());
    }

    private sealed class AcceptingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }
}
