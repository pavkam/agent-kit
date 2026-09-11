// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;



/// <summary>Verifies DefaultLlmModelResolver behavior and contracts.</summary>
public sealed class DefaultLlmModelResolverTests
{
    [Fact]
    public void Resolve_WhenAdapterIsRegistered_ReturnsIt()
    {
        var adapter = new StubModel("chat");
        var resolver = new DefaultLlmModelResolver([adapter]);
        resolver.Resolve(ProviderTestData.Model("chat")).ShouldBeSameAs(adapter);
    }

    [Fact]
    public void Resolve_WhenNoAdapterIsRegisteredForTheAlias_ReturnsNull()
    {
        var resolver = new DefaultLlmModelResolver([new StubModel("chat")]);
        resolver.Resolve(ProviderTestData.Model("other")).ShouldBeNull();
    }

    [Fact]
    public void Resolve_WhenNoAdaptersAtAll_ReturnsNull() => new DefaultLlmModelResolver([]).Resolve(ProviderTestData.Model("chat")).ShouldBeNull();
    [Fact]
    public void Resolve_WhenModelIsNull_ThrowsArgumentNullException()
    {
        var resolver = new DefaultLlmModelResolver([]);
        var exception = Should.Throw<ArgumentNullException>(() => resolver.Resolve(null!));
        exception.ParamName.ShouldBe("model");
    }

    [Fact]
    public void Constructor_WhenTwoAdaptersShareAnAlias_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DefaultLlmModelResolver([new StubModel("dup"), new StubModel("dup")]));
        exception.ParamName.ShouldBe("models");
        exception.Message.ShouldContain("dup");
    }

    [Fact]
    public void Constructor_WhenAdaptersIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultLlmModelResolver(null!));
        exception.ParamName.ShouldBe("models");
    }

    [Fact]
    public void Constructor_WhenAdaptersContainNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DefaultLlmModelResolver([null!]));
        exception.ParamName.ShouldBe("models");
    }

    private sealed class StubModel(string alias): ILlmModel
    {
        public ModelAlias Alias { get; } = new(alias);

        public Task<ModelAttemptResult> ExecuteAsync(LlmModelRequest request, IModelResponseObserver observer, CancellationToken cancellationToken = default) => throw new NotSupportedException("This stub never executes.");
    }
}
