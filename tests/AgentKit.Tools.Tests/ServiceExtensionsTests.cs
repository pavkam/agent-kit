// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void EqualToAnyServiceKey_WhenComparedOrHashed_ReportsPathologicalEqualityAgainstAnything()
    {
        var key = new EqualToAnyServiceKey();

        key.Equals(new object()).ShouldBeTrue();
        key.GetHashCode().ShouldBe(0);

        key.Comparisons.ShouldBe(1);
    }

    [Fact]
    public void AddToolSchemaEngine_WhenRepeated_PreservesExplicitHostChoices()
    {
        var services = new ServiceCollection();
        var selected = new CallbackToolSchemaEngine();
        var expected = new ToolSchemaCompilationRejected(ToolSchemaRejectionReason.ResourceLimitExceeded);
        selected.OnCompile = (_, _, _) => expected;
        var clock = new CallbackTimestampTimeProvider(() => 0);
        _ = services.AddSingleton<IToolSchemaEngine>(selected);
        _ = services.AddSingleton<TimeProvider>(clock);
        services.AddToolSchemaEngine().ShouldBeSameAs(services);
        services.AddToolSchemaEngine().ShouldBeSameAs(services);
        using var host = services.BuildServiceProvider();
        host.GetServices<IToolSchemaEngine>().ShouldBe([selected]);
        host.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
        host.GetRequiredService<IToolSchemaEngine>().Compile(ToolSchemaTestData.Schema("true"), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBeSameAs(expected);
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddToolSchemaEngine()).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).ReplaceToolSchemaEngine<CallbackToolSchemaEngine>()).ParamName.ShouldBe("services");
    }

    [Fact]
    public void ReplaceToolSchemaEngine_WhenSelected_RetainsOldHostsHandlesAndKeys()
    {
        var services = new ServiceCollection().AddToolSchemaEngine();
        using var oldHost = services.BuildServiceProvider();
        var oldEngine = oldHost.GetRequiredService<IToolSchemaEngine>();
        var handle = oldEngine.Compile(ToolSchemaTestData.Schema("true"), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBeOfType<ToolSchemaCompiled>().Schema;
        var keyed = new CallbackToolSchemaEngine();
        _ = services.AddKeyedSingleton<IToolSchemaEngine>("host-key", keyed);
        _ = services.AddSingleton<IToolSchemaEngine>(_ => throw new InvalidOperationException("must-not-activate"));
        services.ReplaceToolSchemaEngine<CallbackToolSchemaEngine>().ShouldBeSameAs(services);
        using var newHost = services.BuildServiceProvider();
        _ = newHost.GetServices<IToolSchemaEngine>().ShouldHaveSingleItem().ShouldBeOfType<CallbackToolSchemaEngine>();
        newHost.GetRequiredKeyedService<IToolSchemaEngine>("host-key").ShouldBeSameAs(keyed);
        oldHost.GetRequiredService<IToolSchemaEngine>().ShouldBeSameAs(oldEngine);
        handle.Validate(ToolSchemaTestData.Instance("null"), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken).ShouldBe(ToolSchemaValidationResult.Valid);
        keyed.Compile(ToolSchemaTestData.Schema("true"), ToolSchemaTestData.Limits, TestContext.Current.CancellationToken)
            .ShouldBeOfType<ToolSchemaCompilationRejected>().Reason.ShouldBe(ToolSchemaRejectionReason.UnsupportedKeyword);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("null")]
    public void AddToolRegistrationCatalog_WhenDiscoveryCatalogNotSingular_RejectsBeforeProviderDiscovery(string problem)
    {
        var services = new ServiceCollection();
        _ = services.AddToolRegistrationCatalog();
        if (problem is "missing" or "null") { _ = services.RemoveAll<IToolRegistrationCatalog>(); }
        if (problem == "duplicate") { _ = services.AddSingleton<IToolRegistrationCatalog>(new CallbackToolRegistrationCatalog()); }
        if (problem == "null") { _ = services.AddSingleton<IToolRegistrationCatalog>(static _ => null!); }
        using var host = services.BuildServiceProvider();
        _ = Should.Throw<InvalidOperationException>(host.GetRequiredService<ToolCatalogDiscovery>);
    }

    [Fact]
    public async Task AddToolRegistrationCatalog_WhenDiscoveryUsesReplacement_PreservesHostSelectionAndClock()
    {
        var services = new ServiceCollection();
        var selections = 0;
        var clock = new CallbackTimestampTimeProvider(static () => 0);
        var catalog = new CallbackToolRegistrationCatalog { SelectRequest = (request, _) => { selections++; return new(request, [], []); } };
        _ = services.AddSingleton<TimeProvider>(clock);
        _ = services.AddSingleton<IToolRegistrationCatalog>(catalog);
        _ = services.AddToolRegistrationCatalog();
        _ = services.AddToolRegistrationCatalog();
        selections.ShouldBe(0);
        using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        host.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
        var discovery = host.GetRequiredService<ToolCatalogDiscovery>();
        discovery.ShouldBeSameAs(host.GetRequiredService<ToolCatalogDiscovery>());
        await using var capture = await discovery.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        selections.ShouldBe(1);
    }

    [Fact]
    public async Task AddToolProvider_WhenGeneric_UsesTypedServiceKeyAndHostOwnedSingleton()
    {
        var source = new ToolSourceId("dynamic");
        var publication = ToolCatalogMergeTestData.Toolset("dynamic-tools", [ToolCatalogMergeTestData.Source("dynamic", [])], []);
        var services = new ServiceCollection();
        _ = services.AddToolset(publication);
        services.AddToolProvider<CallbackToolProvider>(source).ShouldBeSameAs(services);
        var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var provider = host.GetRequiredKeyedService<IToolProvider>(source).ShouldBeOfType<CallbackToolProvider>();
        var catalog = host.GetRequiredService<IToolRegistrationCatalog>();
        var selection = catalog.ResolveSelection(ToolCatalogMergeTestData.Request([publication]), TestContext.Current.CancellationToken);
        selection.Providers.Single().Provider.ShouldBeSameAs(provider);
        host.GetRequiredKeyedService<IToolProvider>(source).ShouldBeSameAs(provider);
        host.GetService<IToolProvider>().ShouldBeNull();
        host.GetKeyedService<IToolProvider>(source.Value).ShouldBeNull();
        provider.Discoveries.ShouldBe(0);
        provider.Disposals.ShouldBe(0);
        await host.DisposeAsync();
        provider.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task AddToolProvider_WhenExistingInstance_RemainsBorrowedAfterHostDisposal()
    {
        var source = new ToolSourceId("borrowed");
        var provider = new CallbackToolProvider(source);
        var services = new ServiceCollection();
        services.AddToolProvider(source, provider).ShouldBeSameAs(services);
        var host = services.BuildServiceProvider();
        _ = host.GetRequiredService<IToolRegistrationCatalog>();
        host.GetRequiredKeyedService<IToolProvider>(source).ShouldBeSameAs(provider);
        await host.DisposeAsync();
        provider.Disposals.ShouldBe(0);
        await provider.DisposeAsync();
        provider.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task ReplaceToolProvider_WhenExistingHostHasSelection_PreservesOldBindingAndPublishesNewProvider()
    {
        var original = ToolCatalogMergeTestData.Candidate();
        var source = original.Source.SourceId;
        var services = new ServiceCollection();
        _ = services.AddToolProvider<CallbackToolProvider>(source);
        _ = services.AddToolset(original.Toolset);
        await using var oldHost = services.BuildServiceProvider();
        var oldCatalog = oldHost.GetRequiredService<IToolRegistrationCatalog>();
        var oldSelection = oldCatalog.ResolveSelection(ToolCatalogMergeTestData.Request([original.Toolset]), TestContext.Current.CancellationToken);
        var oldProvider = oldSelection.Providers.Single().Provider.ShouldBeOfType<CallbackToolProvider>();
        services.ReplaceToolProvider<CallbackToolProvider>(source).ShouldBeSameAs(services);
        var replacement = ToolCatalogMergeTestData.Toolset("tools", [original.Source], original.Toolset.Aliases, version: 2);
        services.ReplaceToolset(replacement).ShouldBeSameAs(services);
        await using var newHost = services.BuildServiceProvider();
        var newSelection = newHost.GetRequiredService<IToolRegistrationCatalog>().ResolveSelection(ToolCatalogMergeTestData.Request([replacement]), TestContext.Current.CancellationToken);
        newSelection.Providers.Single().Provider.ShouldNotBeSameAs(oldProvider);
        newSelection.Toolsets.Single().Version.ShouldBe(new ToolsetVersion(2));
        oldSelection.Toolsets.Single().Version.ShouldBe(new ToolsetVersion(1));
        oldCatalog.ResolveSelection(ToolCatalogMergeTestData.Request([original.Toolset]), TestContext.Current.CancellationToken).Providers.Single().Provider.ShouldBeSameAs(oldProvider);
        oldProvider.Disposals.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddToolProvider_WhenTypedSourceAlreadyRegistered_RejectsWithoutActivationOrMutation(bool existingInstance)
    {
        var source = new ToolSourceId("source");
        var services = new ServiceCollection();
        var activations = 0;
        _ = services.AddKeyedSingleton<IToolProvider>(source, (_, _) => { activations++; throw new InvalidOperationException("must not activate"); });
        ServiceDescriptor[] before = [.. services];
        var error = existingInstance
            ? Should.Throw<ArgumentException>(() => services.AddToolProvider(source, new CallbackToolProvider(source)))
            : Should.Throw<ArgumentException>(() => services.AddToolProvider<CallbackToolProvider>(source));
        error.ParamName.ShouldBe("services");
        services.ShouldBe(before);
        activations.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddToolProvider_WhenArgumentsInvalid_RejectsBeforeMutation(bool replace)
    {
        var source = new ToolSourceId("source");
        var provider = new CallbackToolProvider(source);
        var services = new ServiceCollection();
        IServiceCollection absent = null!;
        void Register(IServiceCollection collection, ToolSourceId key) => _ = replace ? collection.ReplaceToolProvider<CallbackToolProvider>(key) : collection.AddToolProvider<CallbackToolProvider>(key);
        void RegisterInstance(IServiceCollection collection, ToolSourceId key, IToolProvider instance) => _ = replace ? collection.ReplaceToolProvider(key, instance) : collection.AddToolProvider(key, instance);
        Should.Throw<ArgumentNullException>(() => Register(absent, source)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => Register(services, default)).ParamName.ShouldBe("sourceId");
        Should.Throw<ArgumentNullException>(() => RegisterInstance(absent, source, provider)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentOutOfRangeException>(() => RegisterInstance(services, default, provider)).ParamName.ShouldBe("sourceId");
        Should.Throw<ArgumentNullException>(() => RegisterInstance(services, source, null!)).ParamName.ShouldBe("provider");
        provider.IdentityReads.ShouldBe(0);
        Should.Throw<ArgumentException>(() => RegisterInstance(services, new ToolSourceId("SOURCE"), provider)).ParamName.ShouldBe("provider");
        services.ShouldBeEmpty();
        provider.Discoveries.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplaceToolProvider_WhenOpaqueTypedEntriesExist_ReplacesOnlyExactSourceWithoutActivation(bool existingInstance)
    {
        var source = new ToolSourceId("source");
        var alternate = new ToolSourceId("SOURCE");
        var services = new ServiceCollection();
        _ = services.AddToolProvider<CallbackToolProvider>(alternate);
        _ = services.AddKeyedSingleton<IToolProvider>(source, (_, _) => throw new InvalidOperationException("opaque"));
        _ = services.AddKeyedSingleton<IToolProvider>(source, (_, _) => throw new InvalidOperationException("duplicate"));
        var foreignKey = new EqualToAnyServiceKey();
        var foreign = ServiceDescriptor.KeyedSingleton<IToolProvider>(foreignKey, (_, _) => throw new InvalidOperationException("foreign"));
        var unkeyed = ServiceDescriptor.Singleton<IToolProvider>(_ => throw new InvalidOperationException("unkeyed"));
        var stringKeyed = ServiceDescriptor.KeyedSingleton<IToolProvider>(source.Value, (_, _) => throw new InvalidOperationException("string-keyed"));
        _ = services.Add(foreign); _ = services.Add(unkeyed); _ = services.Add(stringKeyed);
        _ = existingInstance ? services.ReplaceToolProvider(source, new CallbackToolProvider(source)) : services.ReplaceToolProvider<CallbackToolProvider>(source);
        services.ShouldContain(foreign); services.ShouldContain(unkeyed); services.ShouldContain(stringKeyed);
        foreignKey.Comparisons.ShouldBe(0);
        services.Count(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IToolProvider) && descriptor.ServiceKey is ToolSourceId key && key == source).ShouldBe(1);
        services.Count(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IToolProvider) && descriptor.ServiceKey is ToolSourceId key && key == alternate).ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task AddToolRegistrationCatalog_WhenSourceCardinalityChanges_RejectsComposition(int count)
    {
        var source = new ToolSourceId("source");
        var services = new ServiceCollection();
        _ = services.AddToolProvider<CallbackToolProvider>(source);
        foreach (var descriptor in services.Where(static descriptor => descriptor.ServiceType == typeof(IToolProvider)).ToArray()) { _ = services.Remove(descriptor); }
        for (var index = 0; index < count; index++) { _ = services.AddKeyedSingleton<IToolProvider, CallbackToolProvider>(source); }
        await using var host = services.BuildServiceProvider();
        _ = Should.Throw<InvalidOperationException>(host.GetRequiredService<IToolRegistrationCatalog>);
    }

    [Fact]
    public async Task AddToolRegistrationCatalog_WhenProviderDeclaresWrongIdentity_RejectsBeforeDiscovery()
    {
        var source = new ToolSourceId("source");
        var services = new ServiceCollection();
        _ = services.AddToolProvider<CallbackToolProvider>(source);
        await using var host = services.BuildServiceProvider();
        var provider = host.GetRequiredKeyedService<IToolProvider>(source).ShouldBeOfType<CallbackToolProvider>();
        provider.ReadSourceId = static () => new ToolSourceId("wrong");
        Should.Throw<ArgumentException>(host.GetRequiredService<IToolRegistrationCatalog>).ParamName.ShouldBe("provider");
        provider.Discoveries.ShouldBe(0);
    }

    [Fact]
    public void AddToolset_WhenDuplicateOrNull_RejectsWithoutMutation()
    {
        var publication = ToolCatalogMergeTestData.Toolset("empty", [], []);
        var services = new ServiceCollection();
        services.AddToolset(publication).ShouldBeSameAs(services);
        ServiceDescriptor[] before = [.. services];
        Should.Throw<ArgumentException>(() => services.AddToolset(publication)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddToolset(null!)).ParamName.ShouldBe("publication");
        Should.Throw<ArgumentNullException>(() => services.ReplaceToolset(null!)).ParamName.ShouldBe("publication");
        IServiceCollection absent = null!;
        Should.Throw<ArgumentNullException>(() => absent.AddToolset(publication)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => absent.ReplaceToolset(publication)).ParamName.ShouldBe("services");
        services.ShouldBe(before);
    }

    [Fact]
    public void ReplaceToolset_WhenTypedFactoriesAndForeignKeysExist_PreservesUnrelatedRegistrations()
    {
        var publication = ToolCatalogMergeTestData.Toolset("tools", [], []);
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<ToolsetPublication>(publication.Key, (_, _) => throw new InvalidOperationException("opaque"));
        _ = services.AddKeyedSingleton<ToolsetPublication>(publication.Key, (_, _) => throw new InvalidOperationException("duplicate"));
        var foreignKey = new EqualToAnyServiceKey();
        var foreign = ServiceDescriptor.KeyedSingleton<ToolsetPublication>(foreignKey, (_, _) => throw new InvalidOperationException("foreign"));
        var stringKeyed = ServiceDescriptor.KeyedSingleton<ToolsetPublication>(publication.Key.Value, (_, _) => throw new InvalidOperationException("string-keyed"));
        var unkeyed = ServiceDescriptor.Singleton<ToolsetPublication>(_ => throw new InvalidOperationException("unkeyed"));
        _ = services.Add(foreign); _ = services.Add(stringKeyed); _ = services.Add(unkeyed);
        services.ReplaceToolset(publication).ShouldBeSameAs(services);
        services.ShouldContain(foreign); services.ShouldContain(stringKeyed); services.ShouldContain(unkeyed);
        foreignKey.Comparisons.ShouldBe(0);
        services.Count(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(ToolsetPublication) && descriptor.ServiceKey is ToolsetKey key && key == publication.Key).ShouldBe(1);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("wrong-key")]
    [InlineData("null")]
    [InlineData("missing-source")]
    public void AddToolRegistrationCatalog_WhenToolsetPublicationInvalid_RejectsComposition(string problem)
    {
        var publication = ToolCatalogMergeTestData.Toolset("tools", [], []);
        var services = new ServiceCollection();
        _ = services.AddToolset(publication);
        if (problem == "missing")
        {
            foreach (var descriptor in services.Where(static descriptor => descriptor.ServiceType == typeof(ToolsetPublication)).ToArray()) { _ = services.Remove(descriptor); }
        }
        else if (problem == "duplicate") { _ = services.AddKeyedSingleton(publication.Key, publication); }
        else if (problem is "wrong-key" or "null")
        {
            foreach (var descriptor in services.Where(static descriptor => descriptor.ServiceType == typeof(ToolsetPublication)).ToArray()) { _ = services.Remove(descriptor); }
            _ = problem == "null"
                ? services.AddKeyedSingleton<ToolsetPublication>(publication.Key, static (_, _) => null!)
                : services.AddKeyedSingleton(publication.Key, ToolCatalogMergeTestData.Toolset("other", [], []));
        }
        else { _ = services.ReplaceToolset(ToolCatalogMergeTestData.Candidate().Toolset); }
        using var host = services.BuildServiceProvider();
        if (problem == "missing-source") { Should.Throw<ArgumentException>(host.GetRequiredService<IToolRegistrationCatalog>).ParamName.ShouldBe("toolsets"); }
        else { _ = Should.Throw<InvalidOperationException>(host.GetRequiredService<IToolRegistrationCatalog>); }
    }

    [Fact]
    public void AddToolRegistrationCatalog_WhenRepeatedOrReplaced_PreservesHostChoicesAndOldViews()
    {
        var services = new ServiceCollection();
        var clock = new CallbackTimestampTimeProvider(static () => 0);
        _ = services.AddSingleton<TimeProvider>(clock);
        services.AddToolRegistrationCatalog().ShouldBeSameAs(services);
        services.AddToolRegistrationCatalog().ShouldBeSameAs(services);
        using var oldHost = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var original = oldHost.GetRequiredService<IToolRegistrationCatalog>();
        var keyed = new CallbackToolRegistrationCatalog();
        _ = services.AddKeyedSingleton<IToolRegistrationCatalog>("host-key", keyed);
        _ = services.AddSingleton<IToolRegistrationCatalog>(_ => throw new InvalidOperationException("must-not-activate"));
        services.ReplaceToolRegistrationCatalog<CallbackToolRegistrationCatalog>().ShouldBeSameAs(services);
        _ = services.AddToolRegistrationCatalog();
        using var newHost = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _ = newHost.GetRequiredService<IToolRegistrationCatalog>().ShouldBeOfType<CallbackToolRegistrationCatalog>();
        newHost.GetRequiredKeyedService<IToolRegistrationCatalog>("host-key").ShouldBeSameAs(keyed);
        newHost.GetServices<IToolRegistrationCatalog>().Count().ShouldBe(1);
        newHost.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
        oldHost.GetRequiredService<IToolRegistrationCatalog>().ShouldBeSameAs(original);
        IServiceCollection absent = null!;
        Should.Throw<ArgumentNullException>(absent.AddToolRegistrationCatalog).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(absent.ReplaceToolRegistrationCatalog<CallbackToolRegistrationCatalog>).ParamName.ShouldBe("services");
    }

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
    public void AddAgentTools_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddAgentTools()).ParamName.ShouldBe("services");

    [Fact]
    public void AddTool_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddTool<AlphaTool>()).ParamName.ShouldBe("services");

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
