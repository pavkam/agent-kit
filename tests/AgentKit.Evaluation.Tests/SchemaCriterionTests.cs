// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class SchemaCriterionTests
{
    internal static JsonSchemaDocument Schema(string json = /*lang=json,strict*/ """{"type":"object","required":["answer"],"properties":{"answer":{"type":"string"}}}""")
    {
        using var document = JsonDocument.Parse(json);
        return new JsonSchemaDocument("answer-schema", new SchemaVersion("1"), document.RootElement);
    }

    [Fact]
    public void Constructor_WhenSchemaIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SchemaCriterion(null!)).ParamName.ShouldBe("schema");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumIssuesIsNotPositive_ThrowsArgumentOutOfRangeException(int issues) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SchemaCriterion(Schema(), null, issues)).ParamName.ShouldBe("maximumIssues");

    [Fact]
    public void Constructor_WhenLimitsAreOmitted_UsesTheDocumentedDefaults()
    {
        var criterion = new SchemaCriterion(Schema());

        criterion.Key.ShouldBe(SchemaCriterion.CriterionKey);
        criterion.Key.Value.ShouldBe("schema");
        criterion.Limits.ShouldBe(new OutputSchemaProcessingLimits(262_144, 64, 100_000));
        criterion.MaximumIssues.ShouldBe(16);
    }

    [Fact]
    public void Equals_WhenSchemaContentMatches_IsEqual() =>
        new SchemaCriterion(Schema()).ShouldBe(new SchemaCriterion(Schema()));
}
