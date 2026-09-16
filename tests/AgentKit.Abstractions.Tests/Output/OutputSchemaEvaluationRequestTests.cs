// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

using System.Text.Json;

/// <summary>Verifies OutputSchemaEvaluationRequest behavior and contracts.</summary>
public sealed class OutputSchemaEvaluationRequestTests
{
    private static readonly JsonSchemaDialectId _dialect = new("urn:test:dialect");
    [Fact]
    public void OutputSchemaEvaluationRequest_WhenSourceDocumentIsDisposed_RetainsCandidateClone()
    {
        using var schemaSource = JsonDocument.Parse("{}");
        using var candidateSource = JsonDocument.Parse("{\"value\":1}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), schemaSource.RootElement);
        var request = new OutputSchemaEvaluationRequest(schema, candidateSource.RootElement, Manifest(), Limits(), Limits(), 4);
        candidateSource.Dispose();
        request.Candidate.GetProperty("value").GetInt32().ShouldBe(1);
    }

    [Fact]
    public void Constructor_WhenSchemaIsNull_ThrowsExactParameter()
    {
        using var candidateSource = JsonDocument.Parse("{}");
        Should.Throw<ArgumentNullException>(() => new OutputSchemaEvaluationRequest(null!, candidateSource.RootElement, Manifest(), Limits(), Limits(), 4)).ParamName.ShouldBe("schema");
    }

    [Fact]
    public void Constructor_WhenCandidateIsUndefined_ThrowsExactParameter()
    {
        using var schemaSource = JsonDocument.Parse("{}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), schemaSource.RootElement);
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaEvaluationRequest(schema, default, Manifest(), Limits(), Limits(), 4)).ParamName.ShouldBe("candidate");
    }

    [Fact]
    public void Constructor_WhenManifestIsNull_ThrowsExactParameter()
    {
        using var schemaSource = JsonDocument.Parse("{}");
        using var candidateSource = JsonDocument.Parse("{}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), schemaSource.RootElement);
        Should.Throw<ArgumentNullException>(() => new OutputSchemaEvaluationRequest(schema, candidateSource.RootElement, null!, Limits(), Limits(), 4)).ParamName.ShouldBe("manifest");
    }

    [Fact]
    public void Constructor_WhenSchemaLimitsIsNull_ThrowsExactParameter()
    {
        using var schemaSource = JsonDocument.Parse("{}");
        using var candidateSource = JsonDocument.Parse("{}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), schemaSource.RootElement);
        Should.Throw<ArgumentNullException>(() => new OutputSchemaEvaluationRequest(schema, candidateSource.RootElement, Manifest(), null!, Limits(), 4)).ParamName.ShouldBe("schemaLimits");
    }

    [Fact]
    public void Constructor_WhenCandidateLimitsIsNull_ThrowsExactParameter()
    {
        using var schemaSource = JsonDocument.Parse("{}");
        using var candidateSource = JsonDocument.Parse("{}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), schemaSource.RootElement);
        Should.Throw<ArgumentNullException>(() => new OutputSchemaEvaluationRequest(schema, candidateSource.RootElement, Manifest(), Limits(), null!, 4)).ParamName.ShouldBe("candidateLimits");
    }

    [Fact]
    public void Constructor_WhenMaximumIssuesIsNotPositive_ThrowsExactParameter()
    {
        using var schemaSource = JsonDocument.Parse("{}");
        using var candidateSource = JsonDocument.Parse("{}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), schemaSource.RootElement);
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaEvaluationRequest(schema, candidateSource.RootElement, Manifest(), Limits(), Limits(), 0)).ParamName.ShouldBe("maximumIssues");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        using var schemaSource = JsonDocument.Parse("{}");
        using var candidateSource = JsonDocument.Parse("{\"value\":1}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), schemaSource.RootElement);
        var manifest = Manifest();
        var schemaLimits = Limits();
        var candidateLimits = Limits();
        var request = new OutputSchemaEvaluationRequest(schema, candidateSource.RootElement, manifest, schemaLimits, candidateLimits, 4);
        request.Schema.ShouldBe(schema);
        request.Manifest.ShouldBe(manifest);
        request.SchemaLimits.ShouldBe(schemaLimits);
        request.CandidateLimits.ShouldBe(candidateLimits);
        request.MaximumIssues.ShouldBe(4);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        using var schemaSource = JsonDocument.Parse("{}");
        using var candidateSource = JsonDocument.Parse("{}");
        var schema = new JsonSchemaDocument("test", new SchemaVersion("v1"), schemaSource.RootElement);
        var original = new OutputSchemaEvaluationRequest(schema, candidateSource.RootElement, Manifest(), Limits(), Limits(), 4);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static OutputSchemaEngineProfile Profile(ImmutableArray<JsonSchemaDialectId>? dialects = null, ImmutableArray<string>? assertions = null, ImmutableArray<string>? annotations = null) => new(new OutputSchemaProfileId("test"), new OutputSchemaProfileVersion(1), _dialect, dialects ?? [_dialect], assertions ?? ["type"], annotations ?? ["title"]);
    private static OutputSchemaProcessingLimits Limits() => new(128, 2, 2);
    private static OutputSchemaPreflightManifest Manifest() => new(Profile(), _dialect, new ContentHash("hash"), Limits(), 1, 1);
}
