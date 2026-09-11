// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;
/// <summary>Verifies JsonSchemaDialectId behavior and contracts.</summary>
public sealed class JsonSchemaDialectIdTests: Conformance.StringIdentityConformanceTests<JsonSchemaDialectId>
{
    [Fact]
    public void JsonSchemaDialectId_WhenTextInvalid_ThrowsExactArgumentException()
    {
        var nullException = Should.Throw<ArgumentNullException>(() => new JsonSchemaDialectId(null!));
        var emptyException = Should.Throw<ArgumentException>(() => new JsonSchemaDialectId(string.Empty));
        var blankException = Should.Throw<ArgumentException>(() => new JsonSchemaDialectId(" \t"));
        nullException.GetType().ShouldBe(typeof(ArgumentNullException));
        emptyException.GetType().ShouldBe(typeof(ArgumentException));
        blankException.GetType().ShouldBe(typeof(ArgumentException));
        nullException.ParamName.ShouldBe("value");
        emptyException.ParamName.ShouldBe("value");
        blankException.ParamName.ShouldBe("value");
    }

    [Fact]
    public void JsonSchemaDialectId_WhenDefault_PreservesPriorValueSemantics()
    {
        default(JsonSchemaDialectId).ShouldBe(default);
        default(JsonSchemaDialectId).ToString().ShouldBeNull();
    }

    /// <inheritdoc/>
    protected override JsonSchemaDialectId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(JsonSchemaDialectId subject) => subject.Value;
}
