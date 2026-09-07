// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

using System.Text.Json;

public sealed class OutputSchemaContractTests
{
    private static readonly OutputSchemaDialectId _dialect = new("urn:test:dialect");

    [Fact]
    public void OutputSchemaEngineProfile_WhenSetsDifferOnlyByOrder_HasStructuralEqualityAndHash()
    {
        var first = Profile([_dialect, new("urn:test:other")], ["type", "required"], ["title", "description"]);
        var second = Profile([new("urn:test:other"), _dialect], ["required", "type"], ["description", "title"]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.SupportedDialects.ShouldBe([_dialect, new("urn:test:other")]);
    }

    [Theory]
    [InlineData("dialects")]
    [InlineData("assertions")]
    [InlineData("annotations")]
    [InlineData("overlap")]
    [InlineData("default")]
    public void OutputSchemaEngineProfile_WhenCapabilitySetsAreInvalid_RejectsExactSet(string invalid)
    {
        ImmutableArray<OutputSchemaDialectId> dialects = invalid switch { "dialects" => [], "default" => [new("urn:test:other")], _ => [_dialect] };
        ImmutableArray<string> assertions = invalid switch { "assertions" => ["type", "type"], "overlap" => ["title"], _ => ["type"] };
        ImmutableArray<string> annotations = invalid == "annotations" ? ["title", "title"] : ["title"];

        var exception = Should.Throw<ArgumentException>(() => Profile(dialects, assertions, annotations));

        exception.ParamName.ShouldBe(invalid switch { "dialects" => "supportedDialects", "assertions" => "assertionKeywords", "annotations" or "overlap" => "annotationKeywords", _ => "defaultDialect" });
    }

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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OutputSchemaProcessingLimits_WhenLimitIsNotPositive_RejectsExactValue(int value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaProcessingLimits(value, 1, 1)).ParamName.ShouldBe("maximumUtf8Bytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaProcessingLimits(1, value, 1)).ParamName.ShouldBe("maximumDepth");
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaProcessingLimits(1, 1, value)).ParamName.ShouldBe("maximumNodes");
    }

    [Fact]
    public void OutputSchemaPreflightManifest_WhenObservedCountsExceedLimits_RejectsExactCount()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaPreflightManifest(Profile(), _dialect, new ContentHash("hash"), Limits(), 3, 1)).ParamName.ShouldBe("observedDepth");
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaPreflightManifest(Profile(), _dialect, new ContentHash("hash"), Limits(), 1, 3)).ParamName.ShouldBe("observedNodes");
    }

    [Fact]
    public void ThrowIfUnsupportedOutputSchemaDialect_WhenUnsupported_UsesInferredParameterName()
    {
        var dialect = new OutputSchemaDialectId("urn:test:unsupported");

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfUnsupportedOutputSchemaDialect(Profile(), dialect));

        exception.ParamName.ShouldBe("dialect");
        Should.Throw<ArgumentNullException>(
            () => ArgumentException.ThrowIfUnsupportedOutputSchemaDialect(null!, _dialect)).ParamName.ShouldBe("profile");
    }

    [Fact]
    public void ClosedResults_WhenRequiredEvidenceIsNull_RejectExactParameter()
    {
        Should.Throw<ArgumentNullException>(() => new OutputSchemaPreflightAccepted(null!)).ParamName.ShouldBe("manifest");
        Should.Throw<ArgumentNullException>(() => new OutputSchemaPreflightRejected(null!)).ParamName.ShouldBe("failure");
        Should.Throw<ArgumentNullException>(() => new OutputSchemaEvaluationConfigurationRejected(null!)).ParamName.ShouldBe("failure");
        Should.Throw<ArgumentException>(() => new OutputSchemaCandidateInvalid([])).ParamName.ShouldBe("issues");
        Should.Throw<ArgumentException>(
            () => new OutputSchemaCandidateInvalid([null!])).ParamName.ShouldBe("issues");
        Should.Throw<ArgumentException>(
            () => new OutputSchemaConfigurationFailure(
                OutputSchemaConfigurationFailureKind.MalformedSchema, "safe", [null!])).ParamName.ShouldBe("issues");
    }

    private static OutputSchemaEngineProfile Profile(
        ImmutableArray<OutputSchemaDialectId>? dialects = null,
        ImmutableArray<string>? assertions = null,
        ImmutableArray<string>? annotations = null) =>
        new(new OutputSchemaProfileId("test"), new OutputSchemaProfileVersion(1), _dialect,
            dialects ?? [_dialect], assertions ?? ["type"], annotations ?? ["title"]);

    private static OutputSchemaProcessingLimits Limits() => new(128, 2, 2);

    private static OutputSchemaPreflightManifest Manifest() =>
        new(Profile(), _dialect, new ContentHash("hash"), Limits(), 1, 1);
}
