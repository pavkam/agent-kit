// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class ServiceExtensionsTests
{
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
