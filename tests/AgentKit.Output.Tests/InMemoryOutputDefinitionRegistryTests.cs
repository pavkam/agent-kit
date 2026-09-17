// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;

public sealed class InMemoryOutputDefinitionRegistryTests
{
    [Fact]
    public void Constructor_WhenComposed_LogsRegistryComposition()
    {
        var id = "definition";
        var v1 = TestFactory.Definition(id: id) with { Version = new OutputDefinitionVersion("1.0") };
        var v2 = TestFactory.Definition(id: id) with { Version = new OutputDefinitionVersion("2.0") };
        var logger = new RecordingLogger<InMemoryOutputDefinitionRegistry>();

        _ = CreateRegistry([v1, v2], logger);

        var entry = logger.Snapshot().Where(static e => e.EventId.Id == 10010).ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.State["DefinitionCount"].ShouldBe(1);
        entry.State["VersionCount"].ShouldBe(2);
    }

    [Fact]
    public void Constructor_WhenDefinitionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new InMemoryOutputDefinitionRegistry(null!, new StructuralOutputSchemaEngine(), DefaultOptions()));

        exception.ParamName.ShouldBe("definitions");
    }

    [Fact]
    public void Constructor_WhenDuplicateIdAndVersion_ThrowsArgumentException()
    {
        var definition = TestFactory.Definition();

        _ = Should.Throw<ArgumentException>(() => CreateRegistry([definition, definition]));
    }

    [Fact]
    public async Task ResolveAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var registry = CreateRegistry([]);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => registry.ResolveAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ResolveAsync_WhenIdIsUnregistered_ReturnsNotFoundAndLogsLookup()
    {
        var logger = new RecordingLogger<InMemoryOutputDefinitionRegistry>();
        var registry = CreateRegistry([], logger);

        var result = await registry.ResolveAsync(
            new OutputDefinitionRequest(new OutputDefinitionId("missing"), null), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<OutputDefinitionNotFound>();
        var entry = logger.Snapshot().Where(static e => e.EventId.Id == 10011).ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Debug);
        entry.State["Outcome"].ShouldBe("not_found");
    }

    [Fact]
    public async Task ResolveAsync_WhenVersionOmitted_ReturnsLatestRegisteredVersion()
    {
        var id = "definition";
        var v1 = TestFactory.Definition(id: id) with { Version = new OutputDefinitionVersion("1.0") };
        var v2 = TestFactory.Definition(id: id) with { Version = new OutputDefinitionVersion("2.0") };
        var registry = CreateRegistry([v1, v2]);

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
        var registry = CreateRegistry([v1, v2]);

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
        var registry = CreateRegistry([v1]);

        var result = await registry.ResolveAsync(
            new OutputDefinitionRequest(new OutputDefinitionId(id), new OutputDefinitionVersion("9.9")),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<OutputDefinitionNotFound>();
    }

    [Fact]
    public void Constructor_WhenRegisteredSchemaIsUnsupported_ThrowsTypedConfigurationException()
    {
        var definition = TestFactory.Definition(
            OutputMode.Prompted,
            schema: TestFactory.Schema(/*lang=json,strict*/"""{"pattern":"secret"}"""));

        var exception = Should.Throw<OutputDefinitionConfigurationException>(() => CreateRegistry([definition]));

        exception.DefinitionId.ShouldBe(definition.Id);
        exception.DefinitionVersion.ShouldBe(definition.Version);
        exception.Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.UnsupportedVocabulary);
    }

    [Fact]
    public void ConfigurationException_WhenDefinitionIdIsDefault_ThrowsArgumentException()
    {
        var failure = SchemaFailure();

        var exception = Should.Throw<ArgumentException>(
            () => new OutputDefinitionConfigurationException(default, new OutputDefinitionVersion("1"), failure));

        exception.ParamName.ShouldBe("definitionId");
    }

    [Fact]
    public void ConfigurationException_WhenDefinitionVersionIsDefault_ThrowsArgumentException()
    {
        var failure = SchemaFailure();

        var exception = Should.Throw<ArgumentException>(
            () => new OutputDefinitionConfigurationException(new OutputDefinitionId("id"), default, failure));

        exception.ParamName.ShouldBe("definitionVersion");
    }

    [Fact]
    public void ConfigurationException_WhenFailureIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new OutputDefinitionConfigurationException(
                new OutputDefinitionId("id"),
                new OutputDefinitionVersion("1"),
                null!));

        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void Constructor_WhenRequiredStructuredSchemaIsMissing_ThrowsTypedConfigurationException()
    {
        var definition = TestFactory.Definition(OutputMode.NativeSchema, schema: null);

        var exception = Should.Throw<OutputDefinitionConfigurationException>(() => CreateRegistry([definition]));

        exception.Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.MalformedSchema);
    }

    [Fact]
    public void Constructor_WhenAnAlternativeSchemaIsUnsupported_ThrowsTypedConfigurationException()
    {
        var definition = TestFactory.Definition(OutputMode.Union) with
        {
            Alternatives = [new OutputAlternative("choice", TestFactory.Schema(/*lang=json,strict*/"""{"pattern":"secret"}"""))],
        };

        var exception = Should.Throw<OutputDefinitionConfigurationException>(() => CreateRegistry([definition]));

        exception.Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.UnsupportedVocabulary);
    }

    [Fact]
    public void Constructor_WhenTextModeDeclaresSchema_ThrowsTypedConfigurationException()
    {
        var definition = TestFactory.Definition(OutputMode.Text, schema: TestFactory.Schema("false"));

        var exception = Should.Throw<OutputDefinitionConfigurationException>(() => CreateRegistry([definition]));

        exception.Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.MalformedSchema);
        exception.Failure.SafeMessage.ShouldBe("A JSON schema cannot be applied to plain-text output.");
    }

    private static InMemoryOutputDefinitionRegistry CreateRegistry(
        IEnumerable<OutputDefinition> definitions, ILogger<InMemoryOutputDefinitionRegistry>? logger = null) =>
        new(definitions, new StructuralOutputSchemaEngine(), DefaultOptions(), logger);

    private static AgentOutputOptionsSnapshot DefaultOptions() =>
        new(1_048_576, 262_144, 64, 4_096, 64, 65_536, 64, 2, true, false);

    private static OutputSchemaConfigurationFailure SchemaFailure() =>
        new(OutputSchemaConfigurationFailureKind.MalformedSchema, "Invalid schema configuration.", []);
}
