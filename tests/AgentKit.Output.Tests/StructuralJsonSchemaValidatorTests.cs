// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

public sealed class StructuralJsonSchemaValidatorTests
{
    [Fact]
    public void Validate_WhenInstanceMatchesType_ReturnsNoIssues()
    {
        var schema = TestFactory.ParseJson(/*lang=json,strict*/"""{"type":"string"}""");
        var instance = TestFactory.ParseJson("\"hello\"");

        var issues = StructuralJsonSchemaValidator.Validate(instance, schema);

        issues.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("\"hello\"", "number")]
    [InlineData("42", "string")]
    [InlineData("true", "object")]
    [InlineData("null", "array")]
    public void Validate_WhenInstanceDoesNotMatchType_ReturnsTypeMismatchIssue(string instanceJson, string type)
    {
        var schema = TestFactory.ParseJson(/*lang=json,strict*/$$"""{"type":"{{type}}"}""");
        var instance = TestFactory.ParseJson(instanceJson);

        var issues = StructuralJsonSchemaValidator.Validate(instance, schema);

        issues.Length.ShouldBe(1);
        issues[0].Code.ShouldBe("type-mismatch");
    }

    [Fact]
    public void Validate_WhenTypeIsAnArrayOfAlternatives_AcceptsAnyMatchingType()
    {
        var schema = TestFactory.ParseJson(/*lang=json,strict*/"""{"type":["string","null"]}""");

        StructuralJsonSchemaValidator.Validate(TestFactory.ParseJson("\"x\""), schema).ShouldBeEmpty();
        StructuralJsonSchemaValidator.Validate(TestFactory.ParseJson("null"), schema).ShouldBeEmpty();
        StructuralJsonSchemaValidator.Validate(TestFactory.ParseJson("42"), schema).ShouldNotBeEmpty();
    }

    [Fact]
    public void Validate_WhenIntegerTypeAndValueHasFraction_ReturnsTypeMismatch()
    {
        var schema = TestFactory.ParseJson(/*lang=json,strict*/"""{"type":"integer"}""");

        StructuralJsonSchemaValidator.Validate(TestFactory.ParseJson("42"), schema).ShouldBeEmpty();
        StructuralJsonSchemaValidator.Validate(TestFactory.ParseJson("42.5"), schema).ShouldNotBeEmpty();
    }

    [Fact]
    public void Validate_WhenRequiredPropertyIsMissing_ReturnsRequiredPropertyMissingIssue()
    {
        var schema = TestFactory.ParseJson(/*lang=json,strict*/"""{"type":"object","required":["name","age"]}""");
        var instance = TestFactory.ParseJson(/*lang=json,strict*/"""{"name":"agent"}""");

        var issues = StructuralJsonSchemaValidator.Validate(instance, schema);

        issues.Length.ShouldBe(1);
        issues[0].Code.ShouldBe("required-property-missing");
        issues[0].Path.ShouldBe("$");
    }

    [Fact]
    public void Validate_WhenAllRequiredPropertiesPresent_ReturnsNoIssues()
    {
        var schema = TestFactory.ParseJson(/*lang=json,strict*/"""{"type":"object","required":["name"]}""");
        var instance = TestFactory.ParseJson(/*lang=json,strict*/"""{"name":"agent"}""");

        StructuralJsonSchemaValidator.Validate(instance, schema).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenNestedPropertyViolatesSchema_ReturnsIssueWithNestedPath()
    {
        var schema = TestFactory.ParseJson(
            /*lang=json,strict*/"""{"type":"object","properties":{"address":{"type":"object","required":["city"]}}}""");
        var instance = TestFactory.ParseJson(/*lang=json,strict*/"""{"address":{}}""");

        var issues = StructuralJsonSchemaValidator.Validate(instance, schema);

        issues.Length.ShouldBe(1);
        issues[0].Path.ShouldBe("$.address");
    }

    [Fact]
    public void Validate_WhenArrayItemViolatesSchema_ReturnsIssueWithIndexedPath()
    {
        var schema = TestFactory.ParseJson(/*lang=json,strict*/"""{"type":"array","items":{"type":"string"}}""");
        var instance = TestFactory.ParseJson(/*lang=json,strict*/"""["a", 2, "c"]""");

        var issues = StructuralJsonSchemaValidator.Validate(instance, schema);

        issues.Length.ShouldBe(1);
        issues[0].Path.ShouldBe("$[1]");
    }

    [Fact]
    public void Validate_WhenSchemaHasNoTypeKeyword_OnlyChecksDeclaredKeywords()
    {
        var schema = TestFactory.ParseJson(/*lang=json,strict*/"""{"required":["name"]}""");
        var instance = TestFactory.ParseJson(/*lang=json,strict*/"""{"name":"agent"}""");

        StructuralJsonSchemaValidator.Validate(instance, schema).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenSchemaIsNotAnObject_ReturnsNoIssues()
    {
        var schema = TestFactory.ParseJson("true");
        var instance = TestFactory.ParseJson(/*lang=json,strict*/"""{"anything":1}""");

        StructuralJsonSchemaValidator.Validate(instance, schema).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenUnknownKeywordPresent_IsIgnored()
    {
        var schema = TestFactory.ParseJson(/*lang=json,strict*/"""{"type":"string","pattern":"^[a-z]+$","format":"email"}""");

        StructuralJsonSchemaValidator.Validate(TestFactory.ParseJson("\"NOT-MATCHING-PATTERN\""), schema).ShouldBeEmpty();
    }
}
