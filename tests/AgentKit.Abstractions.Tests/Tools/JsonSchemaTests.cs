// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

public sealed class JsonSchemaTests
{
    private static readonly JsonSchemaDialectId _dialect = new("https://json-schema.org/draft/2020-12/schema");

    [Fact]
    public void Constructor_WhenDialectDefault_ThrowsArgumentOutOfRangeException()
    {
        using var source = JsonDocument.Parse("true");

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonSchema(default, source.RootElement));

        exception.ParamName.ShouldBe("dialect");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"schema\"")]
    [InlineData("42")]
    [InlineData("[]")]
    public void Constructor_WhenRootIsNotSchemaShape_ThrowsArgumentOutOfRangeException(string json)
    {
        using var source = JsonDocument.Parse(json);

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonSchema(_dialect, source.RootElement));

        exception.ParamName.ShouldBe("document");
    }

    [Fact]
    public void Constructor_WhenDocumentUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonSchema(_dialect, default));

        exception.ParamName.ShouldBe("document");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("{}")]
    [InlineData(/*lang=json,strict*/ "{\"type\":\"object\"}")]
    public void Constructor_WhenRootIsValid_PreservesDialectAndOwnedDocument(string json)
    {
        JsonSchema schema;
        using (var source = JsonDocument.Parse(json))
        {
            schema = new JsonSchema(_dialect, source.RootElement);
        }

        schema.Dialect.ShouldBe(_dialect);
        schema.Document.GetRawText().ShouldBe(json);
    }

    [Fact]
    public void Constructor_WhenDialectDeclarationMatches_PreservesDeclaration()
    {
        const string json = /*lang=json,strict*/ "{\"$schema\":\"https://json-schema.org/draft/2020-12/schema\",\"type\":\"string\"}";
        using var source = JsonDocument.Parse(json);

        var schema = new JsonSchema(_dialect, source.RootElement);

        schema.Document.GetRawText().ShouldBe(json);
    }

    [Fact]
    public void Constructor_WhenDialectDeclarationIsNotString_ThrowsArgumentOutOfRangeException()
    {
        using var source = JsonDocument.Parse(/*lang=json,strict*/ "{\"$schema\":true}");

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonSchema(_dialect, source.RootElement));

        exception.ParamName.ShouldBe("document");
    }

    [Fact]
    public void Constructor_WhenDialectDeclarationDiffers_ThrowsArgumentException()
    {
        using var source = JsonDocument.Parse(/*lang=json,strict*/ "{\"$schema\":\"urn:other\"}");

        var exception = Should.Throw<ArgumentException>(
            () => new JsonSchema(_dialect, source.RootElement));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("document");
    }

    [Fact]
    public void Constructor_WhenDialectDeclarationRepeats_ThrowsArgumentException()
    {
        const string json = "{\"$schema\":\"https://json-schema.org/draft/2020-12/schema\","
            + "\"$schema\":\"https://json-schema.org/draft/2020-12/schema\"}";
        using var source = JsonDocument.Parse(json);

        var exception = Should.Throw<ArgumentException>(
            () => new JsonSchema(_dialect, source.RootElement));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("document");
    }

    [Fact]
    public void Constructor_WhenOrdinaryPropertyRepeats_RetainsRawDocumentForEnginePreflight()
    {
        const string json = /*lang=json,strict*/ "{\"type\":\"string\",\"type\":\"number\"}";
        using var source = JsonDocument.Parse(json);

        var schema = new JsonSchema(_dialect, source.RootElement);

        schema.Document.GetRawText().ShouldBe(json);
        schema.Document.EnumerateObject().Count(static property => property.Name == "type").ShouldBe(2);
    }

    [Fact]
    public void Constructor_WhenSourceDocumentDisposed_ThrowsObjectDisposedException()
    {
        var source = JsonDocument.Parse(/*lang=json,strict*/ "{\"type\":\"object\"}");
        var element = source.RootElement;
        source.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => new JsonSchema(_dialect, element));
    }

    [Fact]
    public void Equality_WhenJsonIsSemanticallyEqual_UsesContentAndCompatibleHash()
    {
        using var firstSource = JsonDocument.Parse(/*lang=json,strict*/ "{\"type\":\"object\",\"required\":[\"id\"]}");
        using var secondSource = JsonDocument.Parse(/*lang=json,strict*/ "{ \"required\" : [ \"id\" ], \"type\" : \"object\" }");
        var first = new JsonSchema(_dialect, firstSource.RootElement);
        var second = new JsonSchema(_dialect, secondSource.RootElement);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Constructor_WhenSchemaAccepted_DoesNotClaimKeywordSupport()
    {
        using var source = JsonDocument.Parse(/*lang=json,strict*/ "{\"futureKeyword\":{\"arbitrary\":true}}");

        var schema = new JsonSchema(_dialect, source.RootElement);

        schema.Document.TryGetProperty("futureKeyword", out var retained).ShouldBeTrue();
        retained.GetProperty("arbitrary").GetBoolean().ShouldBeTrue();
    }
}
