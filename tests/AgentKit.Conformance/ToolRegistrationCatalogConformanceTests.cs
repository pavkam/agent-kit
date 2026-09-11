// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Exercises complete authored selection, exact borrowed providers, cancellation, and independent request evidence.</summary>
public abstract class ToolRegistrationCatalogConformanceTests
{
    /// <summary>Creates the materialized subject through public DI registration.</summary>
    /// <param name="toolsets">The complete immutable publications to register.</param>
    /// <param name="providers">The exact borrowed provider instances to make available under their source keys.</param>
    /// <returns>The catalog under test; the concrete fixture owns its host lifetime.</returns>
    protected abstract IToolRegistrationCatalog CreateCatalog(ImmutableArray<ToolsetPublication> toolsets, ImmutableArray<ToolProviderBinding> providers);

    /// <summary>Authored order wins over registration order, and shared sources appear once.</summary>
    [Fact]
    public void ResolveSelection_WhenToolsetsShareSources_PreservesAuthoredOrderAndExactPolicyVersions()
    {
        var first = ToolCatalogMergeTestData.Candidate("first", "source.first");
        var second = ToolCatalogMergeTestData.Candidate("second", "source.second", "second", "second");
        var shared = ToolCatalogMergeTestData.Toolset("shared", [second.Source, first.Source], [], "shared-policy", 7);
        var a = new CallbackToolProvider(first.Source.SourceId);
        var b = new CallbackToolProvider(second.Source.SourceId);
        var catalog = CreateCatalog([first.Toolset, second.Toolset, shared], [new(first.Source.SourceId, a), new(second.Source.SourceId, b)]);
        var request = ToolCatalogMergeTestData.Request([shared, first.Toolset]);
        var readsA = a.IdentityReads;
        var readsB = b.IdentityReads;
        a.ReadSourceId = static () => throw new InvalidOperationException("selection must not query providers");
        b.ReadSourceId = a.ReadSourceId;

        var selection = catalog.ResolveSelection(request, TestContext.Current.CancellationToken);

        selection.Request.ShouldBeSameAs(request);
        selection.Toolsets.ShouldBe([shared, first.Toolset]);
        selection.Toolsets[0].Version.ShouldBe(new ToolsetVersion(7));
        selection.Toolsets[0].ExecutionPolicy.ShouldBe(shared.ExecutionPolicy);
        selection.Providers.Select(static binding => binding.SourceId).ShouldBe([second.Source.SourceId, first.Source.SourceId]);
        selection.Providers[0].Provider.ShouldBeSameAs(b);
        selection.Providers[1].Provider.ShouldBeSameAs(a);
        a.IdentityReads.ShouldBe(readsA);
        b.IdentityReads.ShouldBe(readsB);
        a.Discoveries.ShouldBe(0);
        b.Discoveries.ShouldBe(0);
        a.Disposals.ShouldBe(0);
        b.Disposals.ShouldBe(0);
    }

    /// <summary>Empty authored selection cannot expose registered providers by default.</summary>
    [Fact]
    public void ResolveSelection_WhenRequestEmpty_ReturnsNoFallbackToolsetOrSource()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var provider = new CallbackToolProvider(candidate.Source.SourceId);
        var catalog = CreateCatalog([candidate.Toolset], [new(candidate.Source.SourceId, provider)]);
        var selection = catalog.ResolveSelection(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        selection.Toolsets.ShouldBeEmpty();
        selection.Providers.ShouldBeEmpty();
        provider.Discoveries.ShouldBe(0);
    }

    /// <summary>Unknown keys and mismatched policy families reject the complete request before discovery.</summary>
    [Fact]
    public void ResolveSelection_WhenAnyAuthoredReferenceUnavailable_ReturnsNoPartialSelection()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var provider = new CallbackToolProvider(candidate.Source.SourceId);
        var catalog = CreateCatalog([candidate.Toolset], [new(candidate.Source.SourceId, provider)]);
        var missing = ToolCatalogMergeTestData.Toolset("missing", [candidate.Source], []);
        var caseMismatch = ToolCatalogMergeTestData.Toolset("TOOLS", [candidate.Source], []);
        var policyMismatch = ToolCatalogMergeTestData.Toolset("tools", [candidate.Source], [], "Standard");
        _ = Should.Throw<InvalidOperationException>(() => catalog.ResolveSelection(ToolCatalogMergeTestData.Request([candidate.Toolset, missing]), TestContext.Current.CancellationToken));
        _ = Should.Throw<InvalidOperationException>(() => catalog.ResolveSelection(ToolCatalogMergeTestData.Request([caseMismatch]), TestContext.Current.CancellationToken));
        _ = Should.Throw<InvalidOperationException>(() => catalog.ResolveSelection(ToolCatalogMergeTestData.Request([policyMismatch]), TestContext.Current.CancellationToken));
        provider.Discoveries.ShouldBe(0);
    }

    /// <summary>Null and cancelled requests preserve exact failure contracts.</summary>
    [Fact]
    public void ResolveSelection_WhenNullOrCancelled_RejectsBeforeTransfer()
    {
        var catalog = CreateCatalog([], []);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var invalid = Should.Throw<ArgumentNullException>(() => catalog.ResolveSelection(null!, cancellation.Token));
        invalid.GetType().ShouldBe(typeof(ArgumentNullException));
        invalid.ParamName.ShouldBe("request");
        var cancelled = Should.Throw<OperationCanceledException>(() => catalog.ResolveSelection(ToolCaptureTestData.Discovery(), cancellation.Token));
        cancelled.CancellationToken.ShouldBe(cancellation.Token);
    }

    /// <summary>Concurrent requests retain their own complete identity and authored selections.</summary>
    [Fact]
    public async Task ResolveSelection_WhenConcurrent_KeepsRequestEvidenceIndependent()
    {
        var first = ToolCatalogMergeTestData.Candidate("first", "source.first");
        var second = ToolCatalogMergeTestData.Candidate("second", "source.second", "second", "second");
        var a = new CallbackToolProvider(first.Source.SourceId);
        var b = new CallbackToolProvider(second.Source.SourceId);
        var catalog = CreateCatalog([first.Toolset, second.Toolset], [new(first.Source.SourceId, a), new(second.Source.SourceId, b)]);
        await Task.WhenAll(Enumerable.Range(0, 24).Select(index => Task.Run(() =>
        {
            var publication = index % 2 == 0 ? first.Toolset : second.Toolset;
            var request = ToolCatalogMergeTestData.Request([publication], $"principal-{index}");
            var selection = catalog.ResolveSelection(request, TestContext.Current.CancellationToken);
            selection.Request.ShouldBeSameAs(request);
            selection.Toolsets.ShouldBe([publication]);
            selection.Providers.Single().Provider.ShouldBeSameAs(index % 2 == 0 ? a : b);
        }, TestContext.Current.CancellationToken)));
        a.Discoveries.ShouldBe(0);
        b.Discoveries.ShouldBe(0);
    }
}
