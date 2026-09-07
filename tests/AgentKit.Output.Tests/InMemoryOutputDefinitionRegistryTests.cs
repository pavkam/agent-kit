// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

public sealed class InMemoryOutputDefinitionRegistryTests
{
    [Fact]
    public void Constructor_WhenDefinitionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemoryOutputDefinitionRegistry(null!));

        exception.ParamName.ShouldBe("definitions");
    }

    [Fact]
    public void Constructor_WhenDuplicateIdAndVersion_ThrowsArgumentException()
    {
        var definition = TestFactory.Definition();

        _ = Should.Throw<ArgumentException>(() => new InMemoryOutputDefinitionRegistry([definition, definition]));
    }

    [Fact]
    public async Task ResolveAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var registry = new InMemoryOutputDefinitionRegistry([]);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => registry.ResolveAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ResolveAsync_WhenIdIsUnregistered_ReturnsNotFound()
    {
        var registry = new InMemoryOutputDefinitionRegistry([]);

        var result = await registry.ResolveAsync(
            new OutputDefinitionRequest(new OutputDefinitionId("missing"), null), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<OutputDefinitionNotFound>();
    }

    [Fact]
    public async Task ResolveAsync_WhenVersionOmitted_ReturnsLatestRegisteredVersion()
    {
        var id = "definition";
        var v1 = TestFactory.Definition(id: id) with { Version = new OutputDefinitionVersion("1.0") };
        var v2 = TestFactory.Definition(id: id) with { Version = new OutputDefinitionVersion("2.0") };
        var registry = new InMemoryOutputDefinitionRegistry([v1, v2]);

        var result = await registry.ResolveAsync(
            new OutputDefinitionRequest(new OutputDefinitionId(id), null), TestContext.Current.CancellationToken);

        var resolved = result.ShouldBeOfType<OutputDefinitionResolved>();
        resolved.Definition.Version.ShouldBe(new OutputDefinitionVersion("2.0"));
    }

    [Fact]
    public async Task ResolveAsync_WhenSpecificVersionRequested_ReturnsThatVersion()
    {
        var id = "definition";
        var v1 = TestFactory.Definition(id: id) with { Version = new OutputDefinitionVersion("1.0") };
        var v2 = TestFactory.Definition(id: id) with { Version = new OutputDefinitionVersion("2.0") };
        var registry = new InMemoryOutputDefinitionRegistry([v1, v2]);

        var result = await registry.ResolveAsync(
            new OutputDefinitionRequest(new OutputDefinitionId(id), new OutputDefinitionVersion("1.0")),
            TestContext.Current.CancellationToken);

        var resolved = result.ShouldBeOfType<OutputDefinitionResolved>();
        resolved.Definition.Version.ShouldBe(new OutputDefinitionVersion("1.0"));
    }

    [Fact]
    public async Task ResolveAsync_WhenSpecificVersionNotRegistered_ReturnsNotFound()
    {
        var id = "definition";
        var v1 = TestFactory.Definition(id: id) with { Version = new OutputDefinitionVersion("1.0") };
        var registry = new InMemoryOutputDefinitionRegistry([v1]);

        var result = await registry.ResolveAsync(
            new OutputDefinitionRequest(new OutputDefinitionId(id), new OutputDefinitionVersion("9.9")),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<OutputDefinitionNotFound>();
    }
}
