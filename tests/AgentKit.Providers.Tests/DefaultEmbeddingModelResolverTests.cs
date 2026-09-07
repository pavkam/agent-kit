// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

/// <summary>
/// Exercises embedding adapter resolution, including the alias-uniqueness
/// rule shared with <see cref="DefaultLlmModelResolver"/>.
/// </summary>
public sealed class DefaultEmbeddingModelResolverTests
{
    [Fact]
    public void Resolve_WhenAdapterIsRegistered_ReturnsIt()
    {
        var adapter = new StubEmbeddingModel("embed");
        var resolver = new DefaultEmbeddingModelResolver([adapter]);

        resolver.Resolve(ProviderTestData.EmbeddingModel("embed")).ShouldBeSameAs(adapter);
    }

    [Fact]
    public void Resolve_WhenNoAdapterIsRegisteredForTheAlias_ReturnsNull()
    {
        var resolver = new DefaultEmbeddingModelResolver([new StubEmbeddingModel("embed")]);

        resolver.Resolve(ProviderTestData.EmbeddingModel("other")).ShouldBeNull();
    }

    [Fact]
    public void Resolve_WhenNoAdaptersAtAll_ReturnsNull() =>
        new DefaultEmbeddingModelResolver([]).Resolve(ProviderTestData.EmbeddingModel("embed")).ShouldBeNull();

    [Fact]
    public void Resolve_WhenModelIsNull_ThrowsArgumentNullException()
    {
        var resolver = new DefaultEmbeddingModelResolver([]);

        var exception = Should.Throw<ArgumentNullException>(() => resolver.Resolve(null!));

        exception.ParamName.ShouldBe("model");
    }

    [Fact]
    public void Constructor_WhenTwoAdaptersShareAnAlias_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new DefaultEmbeddingModelResolver([new StubEmbeddingModel("dup"), new StubEmbeddingModel("dup")]));

        exception.ParamName.ShouldBe("models");
        exception.Message.ShouldContain("dup");
    }

    [Fact]
    public void Constructor_WhenAdaptersIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultEmbeddingModelResolver(null!));

        exception.ParamName.ShouldBe("models");
    }

    [Fact]
    public void Constructor_WhenAdaptersContainNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new DefaultEmbeddingModelResolver([null!]));

        exception.ParamName.ShouldBe("models");
    }

    [Fact]
    public void AddAgentProviders_RegistersTheResolver()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentProviders();
        _ = services.AddSingleton<IEmbeddingModel>(new StubEmbeddingModel("embed"));

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IEmbeddingModelResolver>()
            .Resolve(ProviderTestData.EmbeddingModel("embed"))
            .ShouldNotBeNull();
    }

    private sealed class StubEmbeddingModel(string alias): IEmbeddingModel
    {
        public EmbeddingModelAlias Alias { get; } = new(alias);

        public Task<EmbeddingAttemptResult> GenerateAsync(
            EmbeddingModelRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This stub never executes.");
    }
}
