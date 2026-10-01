// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies profile compilation, ceiling narrowing, validation, and fingerprint stability.</summary>
public sealed class MemoryProfileCatalogTests
{
    private static MemoryProfileCatalog Compile(Action<MemoryProfileOptions> configure, Action<AgentMemoryOptions>? engine = null, QueryRewriterDeclaration[]? rewriters = null)
    {
        var registry = new MemoryProfileRegistry();
        registry.Configure(new MemoryProfileKey("p"), configure, replace: false);
        var options = new AgentMemoryOptions();
        engine?.Invoke(options);
        return new MemoryProfileCatalog(registry, rewriters ?? [new QueryRewriterDeclaration(NoRewriteQueryRewriter.DescriptorValue, typeof(NoRewriteQueryRewriter))], Options.Create(options));
    }

    private static void Valid(MemoryProfileOptions options)
    {
        options.EnableDurableMemory = true;
        options.EnableRetrieval = true;
        options.MemoryStore = new MemoryStoreKey("store");
        options.RetrievalSources = [new RetrievalSourceKey("source")];
        options.MaximumClassification = DataClassification.Internal;
    }

    [Fact]
    public void Constructor_WhenProfileIsValid_CompilesAnExactSnapshot()
    {
        var catalog = Compile(Valid);

        catalog.TryGet(new MemoryProfileKey("p"), out var snapshot).ShouldBeTrue();
        snapshot!.DurableMemoryEnabled.ShouldBeTrue();
        snapshot.RetrievalEnabled.ShouldBeTrue();
        snapshot.MemoryStore.ShouldBe(new MemoryStoreKey("store"));
        snapshot.MaximumClassification.ShouldBe(DataClassification.Internal);
        snapshot.PolicyProfile.ShouldBe(MemoryPolicyProfileKeys.FailClosed);
        snapshot.RetrievalBudget.MaximumItems.ShouldBe(new AgentMemoryOptions().MaximumRetrievedItems);
    }

    [Fact]
    public void TryGet_WhenVersionDiffers_ReturnsFalse()
    {
        var catalog = Compile(Valid);

        catalog.TryGet(new MemoryProfileKey("p"), new MemoryProfileVersion(2), out _).ShouldBeFalse();
        catalog.TryGet(new MemoryProfileKey("p"), new MemoryProfileVersion(1), out _).ShouldBeTrue();
        catalog.TryGet(new MemoryProfileKey("missing"), out _).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenTheSameConfigurationIsCompiledTwice_ProducesTheSameFingerprint()
    {
        var first = Compile(Valid);
        var second = Compile(Valid);

        _ = first.TryGet(new MemoryProfileKey("p"), out var a);
        _ = second.TryGet(new MemoryProfileKey("p"), out var b);

        a!.ConfigurationFingerprint.ShouldBe(b!.ConfigurationFingerprint);
    }

    [Fact]
    public void Constructor_WhenAnySelectedComponentChanges_ChangesTheFingerprint()
    {
        var first = Compile(Valid);
        var second = Compile(options =>
        {
            Valid(options);
            options.RetrievalSources = [new RetrievalSourceKey("other")];
        });

        _ = first.TryGet(new MemoryProfileKey("p"), out var a);
        _ = second.TryGet(new MemoryProfileKey("p"), out var b);

        a!.ConfigurationFingerprint.ShouldNotBe(b!.ConfigurationFingerprint);
    }

    [Fact]
    public void Constructor_WhenCeilingsNarrowTheEngine_UsesTheNarrowerValues()
    {
        var catalog = Compile(options =>
        {
            Valid(options);
            options.MaximumRetrievedItems = 3;
            options.MaximumRetrievedBytes = 1_000;
            options.MaximumRetrievalTokens = 100;
        });

        _ = catalog.TryGet(new MemoryProfileKey("p"), out var snapshot);

        snapshot!.RetrievalBudget.ShouldBe(new RetrievalBudget(3, 1_000, 100));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public void Constructor_WhenItemCeilingWidensOrIsNotPositive_Throws(int requested)
    {
        var exception = Should.Throw<InvalidOperationException>(() => Compile(options =>
        {
            Valid(options);
            options.MaximumRetrievedItems = requested;
        }));

        exception.Message.ShouldContain("MaximumRetrievedItems");
    }

    [Fact]
    public void Constructor_WhenDurableMemoryNamesNoStore_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Compile(options =>
        {
            Valid(options);
            options.MemoryStore = null;
        })).Message.ShouldContain("no memory store");
    }

    [Fact]
    public void Constructor_WhenRetrievalNamesNoSource_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Compile(options =>
        {
            Valid(options);
            options.RetrievalSources = [];
        })).Message.ShouldContain("no retrieval source");
    }

    [Fact]
    public void Constructor_WhenNoClassificationCeilingIsNamed_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Compile(options =>
        {
            Valid(options);
            options.MaximumClassification = null;
        })).Message.ShouldContain("classification");
    }

    [Fact]
    public void Constructor_WhenAPartialEmbeddingTripleIsNamed_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Compile(options =>
        {
            Valid(options);
            options.EmbeddingSelectorKey = new ComponentKey<IEmbeddingModelSelector>("selector");
        })).Message.ShouldContain("partial triple");
    }

    [Fact]
    public void Constructor_WhenRewritingNamesAnUnregisteredRewriter_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Compile(options =>
        {
            Valid(options);
            options.EnableQueryRewriting = true;
            options.QueryRewriter = new QueryRewriterKey("unknown");
        })).Message.ShouldContain("not registered");
    }

    [Fact]
    public void Constructor_WhenProfileDisablesExposureWhileEngineRequiresIt_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Compile(options =>
        {
            Valid(options);
            options.RequireExposureAuthorization = false;
        })).Message.ShouldContain("exposure authorization");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreNull_ThrowArgumentNullException()
    {
        var registry = new MemoryProfileRegistry();

        Should.Throw<ArgumentNullException>(() => new MemoryProfileCatalog(null!, [], Options.Create(new AgentMemoryOptions()))).ParamName.ShouldBe("registry");
        Should.Throw<ArgumentNullException>(() => new MemoryProfileCatalog(registry, null!, Options.Create(new AgentMemoryOptions()))).ParamName.ShouldBe("rewriters");
        Should.Throw<ArgumentNullException>(() => new MemoryProfileCatalog(registry, [], null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Configure_WhenReplacingAProfile_DiscardsEarlierConfiguration()
    {
        var registry = new MemoryProfileRegistry();
        registry.Configure(new MemoryProfileKey("p"), options => options.EnableDurableMemory = true, replace: false);
        registry.Configure(new MemoryProfileKey("p"), options => options.EnableRetrieval = true, replace: true);

        var profile = registry.Profiles.ShouldHaveSingleItem().Value;

        profile.EnableDurableMemory.ShouldBeFalse();
        profile.EnableRetrieval.ShouldBeTrue();
    }

    [Fact]
    public void Configure_WhenAddingToAnExistingProfile_ComposesConfiguration()
    {
        var registry = new MemoryProfileRegistry();
        registry.Configure(new MemoryProfileKey("p"), options => options.EnableDurableMemory = true, replace: false);
        registry.Configure(new MemoryProfileKey("p"), options => options.EnableRetrieval = true, replace: false);

        var profile = registry.Profiles.ShouldHaveSingleItem().Value;

        profile.EnableDurableMemory.ShouldBeTrue();
        profile.EnableRetrieval.ShouldBeTrue();
    }

    [Fact]
    public void Configure_WhenArgumentsAreInvalid_Throws()
    {
        var registry = new MemoryProfileRegistry();

        Should.Throw<ArgumentException>(() => registry.Configure(default, static _ => { }, replace: false)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => registry.Configure(new MemoryProfileKey("p"), null!, replace: false)).ParamName.ShouldBe("configure");
    }
}
