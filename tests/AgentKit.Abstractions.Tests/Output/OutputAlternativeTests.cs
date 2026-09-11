// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

using System.Text.Json;

using AgentKit;

/// <summary>Verifies OutputAlternative behavior and contracts.</summary>
public sealed class OutputAlternativeTests
{
    [Fact]
    public void OutputAlternative_WithInvalidValues_ThrowsAttributedExceptions()
    {
        var alternative = new OutputAlternative("primary", CreateSchema());
        ShouldThrowExact<ArgumentException>(() => _ = alternative with { Name = " " }).ParamName.ShouldBe("Name");
        ShouldThrowExact<ArgumentNullException>(() => _ = alternative with { Schema = null! }).ParamName.ShouldBe("Schema");
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData(" ", typeof(ArgumentException))]
    public void OutputAlternative_ConstructorWhenNameIsInvalid_ThrowsExactException(string? name, Type exceptionType)
    {
        var exception = ShouldThrowExact(() => _ = new OutputAlternative(name!, CreateSchema()), exceptionType);
        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void OutputAlternative_ConstructorWhenSchemaIsNull_ThrowsArgumentNullException()
    {
        var exception = ShouldThrowExact<ArgumentNullException>(() => _ = new OutputAlternative("primary", null!));
        exception.ParamName.ShouldBe("schema");
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData(" ", typeof(ArgumentException))]
    public void OutputAlternative_WithWhenNameIsInvalid_ThrowsExactException(string? name, Type exceptionType)
    {
        var alternative = new OutputAlternative("primary", CreateSchema());
        var exception = ShouldThrowExact(() => _ = alternative with { Name = name! }, exceptionType);
        exception.ParamName.ShouldBe("Name");
    }

    [Fact]
    public void OutputAlternative_WithValidValues_CreatesChangedCopyAndLeavesOriginalUnchanged()
    {
        var schema = CreateSchema();
        var alternative = new OutputAlternative("primary", schema);
        var changed = alternative with
        {
            Name = "secondary"
        };
        changed.Name.ShouldBe("secondary");
        changed.Schema.ShouldBe(schema);
        alternative.Name.ShouldBe("primary");
    }

    private static JsonSchemaDocument CreateSchema()
    {
        using var document = JsonDocument.Parse("true");
        return new JsonSchemaDocument("schema", new SchemaVersion("1"), document.RootElement);
    }

    private static TException ShouldThrowExact<TException>(Action action)
        where TException : Exception
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        return exception;
    }

    private static ArgumentException ShouldThrowExact(Action action, Type exceptionType)
    {
        var exception = Should.Throw<ArgumentException>(action);
        exception.GetType().ShouldBe(exceptionType);
        return exception;
    }
}
