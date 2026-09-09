// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

public sealed class ToolDescriptorTests
{
    [Fact]
    public void Constructor_WhenIdentityDefault_ThrowsArgumentOutOfRangeException()
    {
        var valid = Create();
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolDescriptor(default, valid.Version, valid.Name,
            valid.Description, valid.InputSchema, valid.OutputSchema, valid.Effects, valid.ExecutionHints,
            valid.SourceId, valid.Extensions)).ParamName.ShouldBe("id");
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolDescriptor(valid.Id, default, valid.Name,
            valid.Description, valid.InputSchema, valid.OutputSchema, valid.Effects, valid.ExecutionHints,
            valid.SourceId, valid.Extensions)).ParamName.ShouldBe("version");
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolDescriptor(valid.Id, valid.Version, valid.Name,
            valid.Description, valid.InputSchema, valid.OutputSchema, valid.Effects, valid.ExecutionHints,
            default, valid.Extensions)).ParamName.ShouldBe("sourceId");
    }

    [Fact]
    public void Constructor_WhenTextInvalid_ThrowsExactArgumentException()
    {
        Should.Throw<ArgumentNullException>(() => Create(name: null!)).ParamName.ShouldBe("name");
        Should.Throw<ArgumentException>(() => Create(name: " ")).ParamName.ShouldBe("name");
        Should.Throw<ArgumentNullException>(() => Create(description: null!)).ParamName.ShouldBe("description");
        Should.Throw<ArgumentException>(() => Create(description: " ")).ParamName.ShouldBe("description");
    }

    [Fact]
    public void Constructor_WhenReferenceNull_ThrowsArgumentNullException()
    {
        var valid = Create();
        Should.Throw<ArgumentNullException>(() => new ToolDescriptor(valid.Id, valid.Version, valid.Name,
            valid.Description, null!, valid.OutputSchema, valid.Effects, valid.ExecutionHints,
            valid.SourceId, valid.Extensions)).ParamName.ShouldBe("inputSchema");
        Should.Throw<ArgumentNullException>(() => new ToolDescriptor(valid.Id, valid.Version, valid.Name,
            valid.Description, valid.InputSchema, valid.OutputSchema, null!, valid.ExecutionHints,
            valid.SourceId, valid.Extensions)).ParamName.ShouldBe("effects");
        Should.Throw<ArgumentNullException>(() => new ToolDescriptor(valid.Id, valid.Version, valid.Name,
            valid.Description, valid.InputSchema, valid.OutputSchema, valid.Effects, null!,
            valid.SourceId, valid.Extensions)).ParamName.ShouldBe("executionHints");
        Should.Throw<ArgumentNullException>(() => new ToolDescriptor(valid.Id, valid.Version, valid.Name,
            valid.Description, valid.InputSchema, valid.OutputSchema, valid.Effects, valid.ExecutionHints,
            valid.SourceId, null!)).ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsEveryProperty()
    {
        var output = Schema("false");
        var descriptor = Create(outputSchema: output);

        descriptor.Id.ShouldBe(new ToolId("t"));
        descriptor.Version.ShouldBe(new ToolVersion("1.0"));
        descriptor.Name.ShouldBe("tool");
        descriptor.Description.ShouldBe("description");
        descriptor.InputSchema.ShouldBe(Schema("{}"));
        descriptor.OutputSchema.ShouldBe(output);
        descriptor.Effects.Effect.ShouldBe(ToolEffect.ReadOnly);
        descriptor.ExecutionHints.SchedulingMode.ShouldBe(ToolSchedulingMode.Unspecified);
        descriptor.SourceId.ShouldBe(new ToolSourceId("agentkit.tools.test"));
        descriptor.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void Equality_WhenValuesSame_UsesOwnedSchemaContent()
    {
        Create().ShouldBe(Create());
        Create().GetHashCode().ShouldBe(Create().GetHashCode());
        Create(inputSchema: Schema("{}"))
            .ShouldNotBe(Create(inputSchema: Schema(/*lang=json,strict*/ "{\"type\":\"object\"}")));
    }

    private static ToolDescriptor Create(
        string name = "tool",
        string description = "description",
        JsonSchema? inputSchema = null,
        JsonSchema? outputSchema = null) => new(
            new ToolId("t"), new ToolVersion("1.0"), name, description,
            inputSchema ?? Schema("{}"), outputSchema,
            new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId("agentkit.tools.test"), ExtensionData.Empty);

    private static JsonSchema Schema(string json) => new(
        new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"),
        JsonDocument.Parse(json).RootElement);
}
