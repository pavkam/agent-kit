// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddToolCatalogMerging_WhenPolicyRegistrationsAmbiguous_RejectsInsteadOfSelectingLast()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IToolCatalogMergePolicy>(new CallbackToolCatalogMergePolicy());
        _ = services.AddSingleton<IToolCatalogMergePolicy>(new CallbackToolCatalogMergePolicy());
        _ = services.AddToolCatalogMerging();
        using var host = services.BuildServiceProvider();

        _ = Should.Throw<InvalidOperationException>(host.GetRequiredService<ToolCatalogMerger>);
    }

    [Fact]
    public void AddToolCatalogMerging_WhenRepeated_PreservesHostChoicesWithoutActivation()
    {
        var services = new ServiceCollection();
        var policy = new CallbackToolCatalogMergePolicy();
        var calls = 0;
        var clock = new CallbackTimestampTimeProvider(static () => 0);
        _ = services.AddSingleton<TimeProvider>(clock);
        _ = services.AddSingleton<IToolCatalogMergePolicy>(_ => { calls++; return policy; });
        services.AddToolCatalogMerging().ShouldBeSameAs(services);
        services.AddToolCatalogMerging().ShouldBeSameAs(services);
        calls.ShouldBe(0);
        using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        host.GetRequiredService<IToolCatalogMergePolicy>().ShouldBeSameAs(policy);
        host.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
        host.GetRequiredService<ToolCatalogMerger>().ShouldBeSameAs(host.GetRequiredService<ToolCatalogMerger>());
        host.GetServices<IToolCatalogMergePolicy>().Count().ShouldBe(1);
        calls.ShouldBe(1);
    }

    [Fact]
    public void ReplaceToolCatalogMergePolicy_WhenCalled_ReplacesUnkeyedPoliciesAndPreservesExistingHosts()
    {
        var services = new ServiceCollection();
        _ = services.AddToolCatalogMerging();
        using var oldHost = services.BuildServiceProvider();
        var old = oldHost.GetRequiredService<IToolCatalogMergePolicy>();
        var keyed = new CallbackToolCatalogMergePolicy();
        _ = services.AddKeyedSingleton<IToolCatalogMergePolicy>("host-key", keyed);
        _ = services.AddSingleton<IToolCatalogMergePolicy>(_ => throw new InvalidOperationException("must-not-activate"));
        services.ReplaceToolCatalogMergePolicy<CallbackToolCatalogMergePolicy>().ShouldBeSameAs(services);
        _ = services.AddToolCatalogMerging();
        using var newHost = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _ = newHost.GetRequiredService<IToolCatalogMergePolicy>().ShouldBeOfType<CallbackToolCatalogMergePolicy>();
        newHost.GetRequiredKeyedService<IToolCatalogMergePolicy>("host-key").ShouldBeSameAs(keyed);
        newHost.GetServices<IToolCatalogMergePolicy>().Count().ShouldBe(1);
        oldHost.GetRequiredService<IToolCatalogMergePolicy>().ShouldBeSameAs(old);
    }

    [Fact]
    public void AddToolCatalogMerging_WhenCollectionNull_RejectsExactParameter()
    {
        IServiceCollection services = null!;
        Should.Throw<ArgumentNullException>(services.AddToolCatalogMerging).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(services.ReplaceToolCatalogMergePolicy<CallbackToolCatalogMergePolicy>).ParamName.ShouldBe("services");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddStaticToolProvider_WhenForeignKeyEqualityClaimsMatch_PreservesForeignRegistration(bool replace)
    {
        var snapshot = ToolCaptureTestData.Snapshot([]);
        var services = new ServiceCollection();
        var key = new EqualToAnyServiceKey();
        var foreign = ServiceDescriptor.KeyedSingleton<IToolProvider>(key, (_, _) => throw new InvalidOperationException("unrelated provider"));
        _ = services.Add(foreign);

        var result = replace ? services.ReplaceStaticToolProvider(snapshot, []) : services.AddStaticToolProvider(snapshot, []);

        result.ShouldBeSameAs(services);
        services.ShouldContain(foreign);
        key.Comparisons.ShouldBe(0);
        services.Count(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IToolProvider)
            && descriptor.ServiceKey is ToolSourceId sourceId && sourceId == snapshot.SourceId).ShouldBe(1);
    }

    [Fact]
    public async Task AddStaticToolProvider_WhenConfigured_RegistersExactKeyAndPreservesHostDiagnostics()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var services = new ServiceCollection();
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() => ++reads);
        var logger = new RecordingLogger<StaticToolProvider>();
        var captureLogger = new RecordingLogger<ToolProviderCapture>();
        _ = services.AddSingleton<TimeProvider>(clock);
        _ = services.AddSingleton<ILogger<StaticToolProvider>>(logger);
        _ = services.AddSingleton<ILogger<ToolProviderCapture>>(captureLogger);

        services.AddStaticToolProvider(snapshot, ToolCaptureTestData.Bindings(tool, invoker)).ShouldBeSameAs(services);
        reads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        host.GetService<IToolProvider>().ShouldBeNull();
        host.GetService<IToolCatalog>().ShouldBeNull();
        host.GetKeyedService<IToolProvider>(snapshot.SourceId.Value).ShouldBeNull();
        host.GetKeyedService<IToolProvider>(new ToolSourceId("SOURCE.TESTS")).ShouldBeNull();
        var provider = host.GetRequiredKeyedService<IToolProvider>(snapshot.SourceId);
        provider.ShouldBeSameAs(host.GetRequiredKeyedService<IToolProvider>(new ToolSourceId("source.tests")));
        provider.ShouldBeOfType<StaticToolProvider>().SourceId.ShouldBe(snapshot.SourceId);
        host.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
        await using (var capture = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken))
        {
            await using var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
            lease.Invoker.ShouldBeSameAs(invoker);
        }
        logger.Snapshot().Select(static entry => entry.EventId.Id).ShouldBe([4050, 4051]);
        captureLogger.Snapshot().ShouldNotBeEmpty();
        reads.ShouldBeGreaterThan(0);
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddStaticToolProvider_WhenSourceKeyAlreadyRegistered_RejectsWithoutActivationOrMutation(bool opaque)
    {
        var snapshot = ToolCaptureTestData.Snapshot([]);
        var services = new ServiceCollection();
        var activations = 0;
        _ = opaque
            ? services.AddKeyedSingleton<IToolProvider>(snapshot.SourceId, (_, _) => { activations++; throw new InvalidOperationException("must not activate"); })
            : services.AddStaticToolProvider(snapshot, []);
        var before = services.ToArray();

        var error = Should.Throw<ArgumentException>(() => services.AddStaticToolProvider(snapshot, []));

        error.GetType().ShouldBe(typeof(ArgumentException));
        error.ParamName.ShouldBe("services");
        services.ShouldBe(before);
        activations.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddStaticToolProvider_WhenInputIsInvalid_RejectsBeforeRegistrationChanges(bool replace)
    {
        var tool = ToolCaptureTestData.Descriptor();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var invoker = new CaptureTestToolInvoker();
        var bindings = ToolCaptureTestData.Bindings(tool, invoker);
        var services = new ServiceCollection();
        _ = services.AddSingleton(new object());
        var before = services.ToArray();
        void Register(IServiceCollection collection, ToolProviderSnapshot publication, ImmutableDictionary<ToolIdentity, IToolInvoker> map) => _ = replace ? collection.ReplaceStaticToolProvider(publication, map) : collection.AddStaticToolProvider(publication, map);
        static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
        {
            var error = Should.Throw<TException>(action);
            error.GetType().ShouldBe(typeof(TException));
            error.ParamName.ShouldBe(parameter);
        }

        Exact<ArgumentNullException>(() => Register(null!, snapshot, bindings), "services");
        Exact<ArgumentNullException>(() => Register(services, null!, bindings), "snapshot");
        Exact<ArgumentNullException>(() => Register(services, snapshot, null!), "invokers");
        Exact<ArgumentNullException>(() => Register(services, snapshot, bindings.SetItem(new(tool.Id, tool.Version), null!)), "invokers");
        Exact<ArgumentOutOfRangeException>(() => Register(services, snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker>.Empty.Add(default, invoker)), "invokers");
        Exact<ArgumentException>(() => Register(services, snapshot, []), "invokers");
        services.ShouldBe(before);
        invoker.Disposals.ShouldBe(0);
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ReplaceStaticToolProvider_WhenSourceChanges_PreservesExistingHostsAndCapturedBindings()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var oldInvoker = new CaptureTestToolInvoker();
        var newInvoker = new CaptureTestToolInvoker();
        var oldPublication = ToolCaptureTestData.Snapshot([tool], "old-publication");
        var newPublication = ToolCaptureTestData.Snapshot([tool], "new-publication");
        var services = new ServiceCollection();
        _ = services.AddStaticToolProvider(oldPublication, ToolCaptureTestData.Bindings(tool, oldInvoker));
        await using var oldHost = services.BuildServiceProvider();
        var oldProvider = oldHost.GetRequiredKeyedService<IToolProvider>(oldPublication.SourceId);
        await using var oldCapture = await oldProvider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);

        services.ReplaceStaticToolProvider(newPublication, ToolCaptureTestData.Bindings(tool, newInvoker)).ShouldBeSameAs(services);
        await using var newHost = services.BuildServiceProvider();
        var newProvider = newHost.GetRequiredKeyedService<IToolProvider>(newPublication.SourceId);
        await using var newCapture = await newProvider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        await using var oldLease = (await oldCapture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        await using var newLease = (await newCapture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        oldLease.Invoker.ShouldBeSameAs(oldInvoker);
        oldLease.SourceVersion.ShouldBe(oldPublication.SourceVersion);
        newLease.Invoker.ShouldBeSameAs(newInvoker);
        newLease.SourceVersion.ShouldBe(newPublication.SourceVersion);
        oldHost.GetRequiredKeyedService<IToolProvider>(oldPublication.SourceId).ShouldBeSameAs(oldProvider);
        newProvider.ShouldNotBeSameAs(oldProvider);
    }

    [Fact]
    public async Task ReplaceStaticToolProvider_WhenOpaqueRegistrationsExist_ReplacesOnlyTheExactTypedKey()
    {
        var publication = ToolCaptureTestData.Snapshot([]);
        var alternate = new ToolProviderSnapshot(new ToolSourceId("SOURCE.TESTS"), new ToolSourceVersion("other"), []);
        var services = new ServiceCollection();
        var activations = 0;
        _ = services.AddKeyedSingleton<IToolProvider>(publication.SourceId, (_, _) => { activations++; throw new InvalidOperationException("opaque source"); });
        _ = services.AddKeyedSingleton<IToolProvider>(publication.SourceId, (_, _) => { activations++; throw new InvalidOperationException("duplicate opaque source"); });
        var unkeyed = ServiceDescriptor.Singleton<IToolProvider>(_ => throw new InvalidOperationException("unkeyed source"));
        var stringKeyed = ServiceDescriptor.KeyedSingleton<IToolProvider>(publication.SourceId.Value, (_, _) => throw new InvalidOperationException("string key"));
        _ = services.Add(unkeyed);
        _ = services.Add(stringKeyed);
        _ = services.AddStaticToolProvider(alternate, []);

        _ = services.ReplaceStaticToolProvider(publication, []);
        services.ShouldContain(unkeyed);
        services.ShouldContain(stringKeyed);
        services.Count(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IToolProvider) && Equals(descriptor.ServiceKey, publication.SourceId)).ShouldBe(1);
        await using var host = services.BuildServiceProvider();
        host.GetRequiredKeyedService<IToolProvider>(publication.SourceId).SourceId.ShouldBe(publication.SourceId);
        host.GetRequiredKeyedService<IToolProvider>(alternate.SourceId).SourceId.ShouldBe(alternate.SourceId);
        activations.ShouldBe(0);
    }

    [Fact]
    public void AddAgentTools_WhenCalled_RegistersCatalogAuthorizerAndInvoker()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentTools();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IToolCatalog>().ShouldBeOfType<ToolCatalog>();
        _ = provider.GetRequiredService<IToolAuthorizer>().ShouldBeOfType<AllowListToolAuthorizer>();
        _ = provider.GetRequiredService<IToolInvoker>().ShouldBeOfType<DefaultToolInvoker>();
        _ = provider.GetRequiredService<IToolResultProjectionPolicyCatalog>().ShouldBeOfType<ToolResultProjectionPolicyCatalog>();
    }

    [Fact]
    public void AddAgentTools_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentTools();
        _ = services.AddAgentTools();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<IToolInvoker>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddAgentTools_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentTools(o => o.AllowedToolIds.Add(new ToolId("configured")));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<AgentToolsOptions>>().Value.AllowedToolIds
            .ShouldContain(new ToolId("configured"));
    }

    [Fact]
    public void AddTool_WhenCalledForMultipleTools_RegistersEachAdditively()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentTools();
        _ = services.AddTool<AlphaTool>();
        _ = services.AddTool<BetaTool>();
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<IToolCatalog>();
        catalog.Descriptors.Select(static d => d.Id).ShouldBe([new ToolId("alpha"), new ToolId("beta")], ignoreOrder: true);
    }

    [Fact]
    public void AddToolResultProjectionPolicyCatalog_WhenCalledRepeatedly_KeepsOneCatalogAndTheHostClock()
    {
        // Arrange
        var services = new ServiceCollection();
        var clock = new CallbackTimestampTimeProvider(() => 0);
        _ = services.AddSingleton<TimeProvider>(clock);

        // Act
        services.AddToolResultProjectionPolicyCatalog().ShouldBeSameAs(services);
        _ = services.AddToolResultProjectionPolicyCatalog();
        _ = services.AddAgentTools();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        // Assert
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
        var catalogs = provider.GetServices<IToolResultProjectionPolicyCatalog>().ToArray();
        catalogs.Length.ShouldBe(1);
        catalogs[0].ShouldBeSameAs(provider.GetRequiredService<IToolResultProjectionPolicyCatalog>());
        provider.GetServices<ToolResultProjectionPolicySnapshot>().ShouldBeEmpty();
    }

    [Fact]
    public void AddToolResultProjectionPolicyCatalog_WhenServiceCollectionIsNull_RejectsExactParameter()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act / Assert
        Should.Throw<ArgumentNullException>(services.AddToolResultProjectionPolicyCatalog).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddToolResultProjectionPolicy(ToolProjectionPolicyTestData.Snapshot())).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddToolResultProjectionPolicy_WhenSnapshotIsNull_RejectsBeforeRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var exception = Should.Throw<ArgumentNullException>(() => services.AddToolResultProjectionPolicy(null!));

        // Assert
        exception.ParamName.ShouldBe("snapshot");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddToolResultProjectionPolicy_WhenRevisionsAndEquivalentDuplicatesAreAdded_CapturesThemExactly()
    {
        // Arrange
        var services = new ServiceCollection();
        var older = ToolProjectionPolicyTestData.Snapshot(version: 1);
        var newer = ToolProjectionPolicyTestData.Snapshot(version: 2, maximumBytes: 512);

        // Act
        services.AddToolResultProjectionPolicy(older).ShouldBeSameAs(services);
        _ = services.AddToolResultProjectionPolicy(newer);
        _ = services.AddToolResultProjectionPolicy(ToolProjectionPolicyTestData.Snapshot(version: 1));
        await using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IToolResultProjectionPolicyCatalog>();

        // Assert
        (await catalog.ResolveAsync(older.Reference, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBe(older);
        (await catalog.ResolveAsync(newer.Reference, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBe(newer);
    }

    [Fact]
    public void AddToolResultProjectionPolicy_WhenAReferenceHasConflictingContent_RejectsCatalogComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddToolResultProjectionPolicy(ToolProjectionPolicyTestData.Snapshot());
        _ = services.AddToolResultProjectionPolicy(ToolProjectionPolicyTestData.Snapshot(maximumBytes: 512));
        using var provider = services.BuildServiceProvider();

        // Act
        var exception = Should.Throw<ArgumentException>(provider.GetRequiredService<IToolResultProjectionPolicyCatalog>);

        // Assert
        exception.ParamName.ShouldBe("policies");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddToolResultProjectionPolicyCatalog_WhenHostReplacesTheCatalog_HonorsTheExplicitRegistration(bool beforeDefaults)
    {
        // Arrange
        var services = new ServiceCollection();
        var replacement = new StubProjectionPolicyCatalog();
        var activated = false;
        var descriptor = ServiceDescriptor.Singleton<IToolResultProjectionPolicyCatalog>(_ =>
        {
            activated = true;
            return replacement;
        });

        // Act
        if (beforeDefaults)
        {
            _ = services.Add(descriptor);
            _ = services.AddAgentTools();
        }
        else
        {
            _ = services.AddAgentTools();
            _ = services.Replace(descriptor);
        }

        _ = services.AddToolResultProjectionPolicy(ToolProjectionPolicyTestData.Snapshot());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        // Assert
        activated.ShouldBeFalse();
        provider.GetRequiredService<IToolResultProjectionPolicyCatalog>().ShouldBeSameAs(replacement);
        activated.ShouldBeTrue();
        provider.GetServices<IToolResultProjectionPolicyCatalog>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task ReplaceToolResultProjectionPolicy_WhenOneRevisionChanges_PreservesOtherRevisionsAndCapturedCatalogs()
    {
        // Arrange
        var first = ToolProjectionPolicyTestData.Snapshot();
        var second = ToolProjectionPolicyTestData.Snapshot(version: 2);
        var replacement = ToolProjectionPolicyTestData.Snapshot(maximumBytes: 512);
        var services = new ServiceCollection();
        _ = services.AddToolResultProjectionPolicy(first);
        _ = services.AddToolResultProjectionPolicy(first);
        _ = services.AddToolResultProjectionPolicy(second);
        await using var originalProvider = services.BuildServiceProvider();
        var originalCatalog = originalProvider.GetRequiredService<IToolResultProjectionPolicyCatalog>();

        // Act
        services.ReplaceToolResultProjectionPolicy(replacement).ShouldBeSameAs(services);
        await using var updatedProvider = services.BuildServiceProvider();
        var updatedCatalog = updatedProvider.GetRequiredService<IToolResultProjectionPolicyCatalog>();

        // Assert
        updatedProvider.GetServices<ToolResultProjectionPolicySnapshot>().ShouldBe([second, replacement]);
        (await originalCatalog.ResolveAsync(first.Reference, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBe(first);
        (await updatedCatalog.ResolveAsync(first.Reference, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBe(replacement);
        (await updatedCatalog.ResolveAsync(second.Reference, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBe(second);
    }

    [Fact]
    public async Task ReplaceToolResultProjectionPolicy_WhenReferenceIsAbsent_AddsTheExplicitSnapshot()
    {
        // Arrange
        var services = new ServiceCollection();
        var policy = ToolProjectionPolicyTestData.Snapshot();

        // Act
        _ = services.ReplaceToolResultProjectionPolicy(policy);
        await using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IToolResultProjectionPolicyCatalog>().ResolveAsync(policy.Reference, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBe(policy);
    }

    [Fact]
    public void ReplaceToolResultProjectionPolicy_WhenAnExistingReferenceRequiresActivation_RejectsBeforeMutation()
    {
        // Arrange
        var services = new ServiceCollection();
        var activated = false;
        _ = services.AddSingleton(_ =>
        {
            activated = true;
            return ToolProjectionPolicyTestData.Snapshot();
        });
        ServiceDescriptor[] original = [.. services];

        // Act
        var exception = Should.Throw<ArgumentException>(() => services.ReplaceToolResultProjectionPolicy(ToolProjectionPolicyTestData.Snapshot()));

        // Assert
        exception.ParamName.ShouldBe("services");
        activated.ShouldBeFalse();
        services.ShouldBe(original);
    }

    [Fact]
    public void ReplaceToolResultProjectionPolicyCatalog_WhenCalled_RemovesEarlierBindingsAndPreservesSnapshots()
    {
        // Arrange
        var services = new ServiceCollection();
        var policy = ToolProjectionPolicyTestData.Snapshot();
        _ = services.AddToolResultProjectionPolicy(policy);
        _ = services.AddSingleton<IToolResultProjectionPolicyCatalog>(_ => throw new InvalidOperationException("removed registration"));

        // Act
        services.ReplaceToolResultProjectionPolicyCatalog<StubProjectionPolicyCatalog>().ShouldBeSameAs(services);
        _ = services.AddAgentTools();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        // Assert
        var catalog = provider.GetRequiredService<IToolResultProjectionPolicyCatalog>();
        _ = catalog.ShouldBeOfType<StubProjectionPolicyCatalog>();
        provider.GetServices<IToolResultProjectionPolicyCatalog>().ShouldBe([catalog]);
        provider.GetServices<ToolResultProjectionPolicySnapshot>().ShouldBe([policy]);
    }

    [Fact]
    public void ReplacementRegistrations_WhenArgumentsAreNull_RejectBeforeMutation()
    {
        // Arrange
        IServiceCollection absent = null!;
        var services = new ServiceCollection();

        // Act / Assert
        Should.Throw<ArgumentNullException>(() => absent.ReplaceToolResultProjectionPolicy(ToolProjectionPolicyTestData.Snapshot())).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(absent.ReplaceToolResultProjectionPolicyCatalog<StubProjectionPolicyCatalog>).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.ReplaceToolResultProjectionPolicy(null!)).ParamName.ShouldBe("snapshot");
        services.ShouldBeEmpty();
    }

    private sealed class AlphaTool: ITool
    {
        public ToolDescriptor Descriptor { get; } = TestFactory.Descriptor("alpha");

        public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class BetaTool: ITool
    {
        public ToolDescriptor Descriptor { get; } = TestFactory.Descriptor("beta");

        public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
