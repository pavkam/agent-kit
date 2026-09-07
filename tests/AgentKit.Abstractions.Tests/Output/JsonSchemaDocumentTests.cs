// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

using System.Text.Json;

using AgentKit;

public sealed class JsonSchemaDocumentTests
{
    [Fact]
    public void Constructor_WhenSourceDocumentDisposed_RetainsReadableSchema()
    {
        JsonSchemaDocument schema;
        using (var source = JsonDocument.Parse("""{"type":"object","properties":{"name":{"type":"string"}}}"""))
        {
            schema = new JsonSchemaDocument("person", new SchemaVersion("v1"), source.RootElement);
        }

        schema.Schema.GetProperty("properties").GetProperty("name").GetProperty("type").GetString().ShouldBe("string");
    }

    [Fact]
    public void With_WhenSourceDocumentDisposed_ClonesReplacementAndDoesNotChangeOriginal()
    {
        JsonSchemaDocument original;
        JsonSchemaDocument replacement;

        using (var originalSource = JsonDocument.Parse("""{"type":"string"}"""))
        using (var replacementSource = JsonDocument.Parse("""{"type":"object","properties":{"count":{"type":"integer"}}}"""))
        {
            original = new JsonSchemaDocument("original", new SchemaVersion("v1"), originalSource.RootElement);
            replacement = original with
            {
                Name = "replacement",
                Schema = replacementSource.RootElement,
            };
        }

        original.Name.ShouldBe("original");
        original.Schema.GetProperty("type").GetString().ShouldBe("string");
        replacement.Name.ShouldBe("replacement");
        replacement.Schema.GetProperty("properties").GetProperty("count").GetProperty("type").GetString().ShouldBe("integer");
    }

    [Fact]
    public void Equality_WhenSchemasReconstructedWithDifferentPropertyOrder_IsStructuralAndHashesMatch()
    {
        using var firstSource = JsonDocument.Parse("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""");
        using var secondSource = JsonDocument.Parse("""{"required":["name"],"properties":{"name":{"type":"string"}},"type":"object"}""");
        var first = new JsonSchemaDocument("person", new SchemaVersion("v1"), firstSource.RootElement);
        var second = new JsonSchemaDocument("person", new SchemaVersion("v1"), secondSource.RootElement);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenNameVersionOrSchemaDiffer_InstancesAreNotEqual()
    {
        using var source = JsonDocument.Parse("""{"type":"string"}""");
        using var otherSource = JsonDocument.Parse("""{"type":"number"}""");
        var original = new JsonSchemaDocument("schema", new SchemaVersion("v1"), source.RootElement);

        (original with { Name = "other" }).ShouldNotBe(original);
        (original with { Version = new SchemaVersion("v2") }).ShouldNotBe(original);
        (original with { Schema = otherSource.RootElement }).ShouldNotBe(original);
    }

    [Fact]
    public void Constructor_WhenNameNull_ThrowsArgumentNullExceptionWithParameterName()
    {
        using var source = JsonDocument.Parse("{}");

        var exception = Should.Throw<ArgumentNullException>(() => new JsonSchemaDocument(null!, new SchemaVersion("v1"), source.RootElement));

        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void Constructor_WhenNameWhitespace_ThrowsArgumentExceptionWithParameterName()
    {
        using var source = JsonDocument.Parse("{}");

        var exception = Should.Throw<ArgumentException>(() => new JsonSchemaDocument(" ", new SchemaVersion("v1"), source.RootElement));

        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void Constructor_WhenVersionDefault_ThrowsArgumentOutOfRangeExceptionWithParameterName()
    {
        using var source = JsonDocument.Parse("{}");

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonSchemaDocument("schema", default, source.RootElement));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenSchemaUndefined_ThrowsArgumentOutOfRangeExceptionWithParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonSchemaDocument("schema", new SchemaVersion("v1"), default));

        exception.ParamName.ShouldBe("schema");
    }

    [Fact]
    public void Constructor_WhenSchemaSourceAlreadyDisposed_ThrowsObjectDisposedException()
    {
        JsonElement disposedSchema;
        using (var source = JsonDocument.Parse("{}"))
        {
            disposedSchema = source.RootElement;
        }

        _ = Should.Throw<ObjectDisposedException>(() => new JsonSchemaDocument("schema", new SchemaVersion("v1"), disposedSchema));
    }

    [Fact]
    public void With_WhenNameInvalid_ThrowsArgumentExceptionWithPropertyParameterName()
    {
        var schema = CreateSchema();

        var exception = Should.Throw<ArgumentException>(() => _ = schema with { Name = " " });

        exception.ParamName.ShouldBe("Name");
    }

    [Fact]
    public void With_WhenNameNull_ThrowsArgumentNullExceptionWithPropertyParameterName()
    {
        var schema = CreateSchema();

        var exception = Should.Throw<ArgumentNullException>(() => _ = schema with { Name = null! });

        exception.ParamName.ShouldBe("Name");
    }

    [Fact]
    public void With_WhenVersionDefault_ThrowsArgumentOutOfRangeExceptionWithPropertyParameterName()
    {
        var schema = CreateSchema();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => _ = schema with { Version = default });

        exception.ParamName.ShouldBe("Version");
    }

    [Fact]
    public void With_WhenSchemaUndefined_ThrowsArgumentOutOfRangeExceptionWithPropertyParameterName()
    {
        var schema = CreateSchema();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => _ = schema with { Schema = default });

        exception.ParamName.ShouldBe("Schema");
    }

    [Fact]
    public void With_WhenSchemaSourceAlreadyDisposed_ThrowsObjectDisposedExceptionAndPreservesOriginal()
    {
        var original = CreateSchema();
        JsonElement disposedSchema;
        using (var source = JsonDocument.Parse("""{"type":"number"}"""))
        {
            disposedSchema = source.RootElement;
        }

        _ = Should.Throw<ObjectDisposedException>(() => _ = original with { Schema = disposedSchema });

        original.Schema.GetProperty("type").GetString().ShouldBe("object");
    }

    private static JsonSchemaDocument CreateSchema()
    {
        using var source = JsonDocument.Parse("""{"type":"object"}""");
        return new JsonSchemaDocument("schema", new SchemaVersion("v1"), source.RootElement);
    }
}
