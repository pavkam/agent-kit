// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

using System.Text.Json;

/// <summary>Verifies OutputSchemaPreflightRequest behavior and contracts.</summary>
public sealed class OutputSchemaPreflightRequestTests
{
    [Fact]
    public void Constructor_WhenSchemaIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputSchemaPreflightRequest(null!, OutputTestData.Limits())).ParamName.ShouldBe("schema");

    [Fact]
    public void Constructor_WhenLimitsIsNull_ThrowsExactParameter()
    {
        using var document = JsonDocument.Parse("{}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), document.RootElement);
        Should.Throw<ArgumentNullException>(() => new OutputSchemaPreflightRequest(schema, null!)).ParamName.ShouldBe("limits");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        using var document = JsonDocument.Parse("{}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), document.RootElement);
        var limits = OutputTestData.Limits();
        var request = new OutputSchemaPreflightRequest(schema, limits);
        request.Schema.ShouldBe(schema);
        request.Limits.ShouldBe(limits);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        using var document = JsonDocument.Parse("{}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), document.RootElement);
        var original = new OutputSchemaPreflightRequest(schema, OutputTestData.Limits());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
