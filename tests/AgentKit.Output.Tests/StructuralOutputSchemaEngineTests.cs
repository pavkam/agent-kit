// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

/// <summary>Verifies the bounded fail-closed structural schema profile.</summary>
public sealed class StructuralOutputSchemaEngineTests
{
    private static readonly OutputSchemaProcessingLimits Limits = new(4096, 16, 128);
    private readonly StructuralOutputSchemaEngine _engine = new();

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(/*lang=json,strict*/"""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"],"additionalProperties":false,"title":"record"}""")]
    [InlineData(/*lang=json,strict*/"""{"$schema":"urn:agentkit:json-schema:structural:v1","type":"array","items":{"type":"integer"}}""")]
    public void Preflight_WhenSchemaUsesStructuralProfile_ReturnsManifest(string json)
    {
        var result = _engine.Preflight(Request(json), TestContext.Current.CancellationToken);

        var manifest = result.ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest;
        manifest.Dialect.ShouldBe(_engine.Profile.DefaultDialect);
        manifest.ObservedDepth.ShouldBeGreaterThan(0);
        manifest.ObservedNodes.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("true", "sha256:b5bea41b6c623f7c09f1bf24dcae58ebab3c0cdd90ad966bc43a45b44867e12b")]
    [InlineData(/*lang=json,strict*/"""{"title":"<é>","default":1.00}""", "sha256:6305be41c976a1b3dda22dd56139290b16b29dfeeb40df1b8684ceefd99faf82")]
    [InlineData(/*lang=json,strict*/"""{"properties":{"<é>":true},"default":1.00}""", "sha256:cfa808528c56f44e4e3c40823ab746cd8cb82c6279a201bc8f77bba26e62faf9")]
    public void Preflight_WhenSchemaIsCanonicalized_ProducesPinnedFingerprint(string json, string expectedFingerprint)
    {
        var result = _engine.Preflight(Request(json), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest.SchemaFingerprint.Value.ShouldBe(expectedFingerprint);
    }

    [Theory]
    [InlineData(/*lang=json,strict*/"""{"pattern":"x"}""", OutputSchemaConfigurationFailureKind.UnsupportedVocabulary)]
    [InlineData(/*lang=json,strict*/"""{"$schema":"https://json-schema.org/draft/2020-12/schema"}""", OutputSchemaConfigurationFailureKind.UnsupportedDialect)]
    [InlineData(/*lang=json,strict*/"""{"properties":{"x":{"$schema":"urn:agentkit:json-schema:structural:v1"}}}""", OutputSchemaConfigurationFailureKind.UnsupportedDialect)]
    [InlineData(/*lang=json,strict*/"""{"additionalProperties":{}}""", OutputSchemaConfigurationFailureKind.MalformedSchema)]
    [InlineData(/*lang=json,strict*/"""{"type":["string","string"]}""", OutputSchemaConfigurationFailureKind.MalformedSchema)]
    [InlineData(/*lang=json,strict*/"""{"required":["x","x"]}""", OutputSchemaConfigurationFailureKind.MalformedSchema)]
    [InlineData(/*lang=json,strict*/"""{"type":"string","type":"number"}""", OutputSchemaConfigurationFailureKind.MalformedSchema)]
    [InlineData(/*lang=json,strict*/"""{"properties":{"x":true,"x":false}}""", OutputSchemaConfigurationFailureKind.MalformedSchema)]
    public void Preflight_WhenSchemaIsUnsupportedOrMalformed_ReturnsConfigurationFailure(
        string json,
        OutputSchemaConfigurationFailureKind kind)
    {
        var result = _engine.Preflight(Request(json), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputSchemaPreflightRejected>().Failure.Kind.ShouldBe(kind);
    }

    [Fact]
    public void Preflight_WhenSchemaExceedsBounds_ReturnsResourceLimitFailure()
    {
        var request = new OutputSchemaPreflightRequest(TestFactory.Schema(/*lang=json,strict*/"""{"description":"too large"}"""), new OutputSchemaProcessingLimits(8, 16, 128));

        var result = _engine.Preflight(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputSchemaPreflightRejected>().Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.ResourceLimitExceeded);
    }

    [Fact]
    public void Preflight_WhenRequestedDepthExceedsEngineSafetyLimit_ReturnsResourceLimitFailure()
    {
        var request = new OutputSchemaPreflightRequest(TestFactory.Schema("true"), new OutputSchemaProcessingLimits(4096, 129, 128));

        var result = _engine.Preflight(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputSchemaPreflightRejected>().Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.ResourceLimitExceeded);
    }

    [Fact]
    public void Preflight_WhenAnnotationValuesExceedNodeLimit_ReturnsResourceLimitFailure()
    {
        var request = new OutputSchemaPreflightRequest(
            TestFactory.Schema(/*lang=json,strict*/"""{"examples":[1,2,3]}"""),
            new OutputSchemaProcessingLimits(4096, 16, 3));

        var result = _engine.Preflight(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputSchemaPreflightRejected>().Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.ResourceLimitExceeded);
    }

    [Fact]
    public void Preflight_WhenAnnotationReachesMaximumSupportedDepth_ReturnsExactObservedDepth()
    {
        var nestedValue = new string('[', 126) + "null" + new string(']', 126);
        using var document = JsonDocument.Parse(
            $$"""{"default":{{nestedValue}}}""",
            new JsonDocumentOptions { MaxDepth = 128 });
        var request = new OutputSchemaPreflightRequest(
            new JsonSchemaDocument("deep-schema", new SchemaVersion("1.0"), document.RootElement),
            new OutputSchemaProcessingLimits(4096, 128, 128));

        var result = _engine.Preflight(request, TestContext.Current.CancellationToken);

        var manifest = result.ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest;
        manifest.ObservedDepth.ShouldBe(128);
        manifest.ObservedNodes.ShouldBe(128);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, false)]
    public void Preflight_WhenNodeCountMeetsOrExceedsLimit_EnforcesInclusiveBound(
        int maximumNodes,
        bool expectedAccepted)
    {
        var request = new OutputSchemaPreflightRequest(
            TestFactory.Schema(/*lang=json,strict*/"""{"examples":[1,2]}"""),
            new OutputSchemaProcessingLimits(4096, 16, maximumNodes));

        var result = _engine.Preflight(request, TestContext.Current.CancellationToken);

        if (expectedAccepted)
        {
            result.ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest.ObservedNodes.ShouldBe(4);
        }
        else
        {
            result.ShouldBeOfType<OutputSchemaPreflightRejected>().Failure.Kind
                .ShouldBe(OutputSchemaConfigurationFailureKind.ResourceLimitExceeded);
        }
    }

    [Fact]
    public void Preflight_WhenWideAnnotationExceedsSmallNodeLimit_ReturnsResourceLimitFailure()
    {
        var values = string.Join(',', Enumerable.Repeat("null", 10_000));
        var request = new OutputSchemaPreflightRequest(
            TestFactory.Schema($$"""{"default":[{{values}}]}"""),
            new OutputSchemaProcessingLimits(100_000, 16, 3));

        var result = _engine.Preflight(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputSchemaPreflightRejected>().Failure.Kind
            .ShouldBe(OutputSchemaConfigurationFailureKind.ResourceLimitExceeded);
    }

    [Theory]
    [InlineData("annotation")]
    [InlineData("property")]
    [InlineData("number")]
    public void Preflight_WhenSingleTokenCannotFitByteLimit_ReturnsResourceLimitFailure(string tokenKind)
    {
        var oversizedToken = new string(tokenKind == "number" ? '1' : 'x', 100_000);
        var json = tokenKind switch
        {
            "annotation" => "{\"description\":\"" + oversizedToken + "\"}",
            "property" => "{\"properties\":{\"" + oversizedToken + "\":true}}",
            "number" => "{\"default\":" + oversizedToken + "}",
            _ => throw new UnreachableException(),
        };
        var request = new OutputSchemaPreflightRequest(
            TestFactory.Schema(json),
            new OutputSchemaProcessingLimits(64, 16, 16));

        var result = _engine.Preflight(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputSchemaPreflightRejected>().Failure.Kind
            .ShouldBe(OutputSchemaConfigurationFailureKind.ResourceLimitExceeded);
    }

    [Theory]
    [InlineData(/*lang=json,strict*/"""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"],"additionalProperties":false}""", /*lang=json,strict*/"""{"name":"agent"}""")]
    [InlineData(/*lang=json,strict*/"""{"type":"integer"}""", "1.000")]
    [InlineData(/*lang=json,strict*/"""{"type":"integer"}""", "1e100000")]
    [InlineData(/*lang=json,strict*/"""{"type":"integer"}""", "0e-100000")]
    [InlineData(/*lang=json,strict*/"""{"type":"object","additionalProperties":true}""", /*lang=json,strict*/"""{"anything":1}""")]
    public void Evaluate_WhenCandidateConforms_ReturnsPassed(string schemaJson, string candidateJson)
    {
        var result = Evaluate(schemaJson, candidateJson);

        _ = result.ShouldBeOfType<OutputSchemaEvaluationPassed>();
    }

    [Theory]
    [InlineData(/*lang=json,strict*/"""{"type":"object","properties":{"name":{"type":"string"}},"additionalProperties":false}""", /*lang=json,strict*/"""{"name":"agent","extra":true}""", "additional-property")]
    [InlineData(/*lang=json,strict*/"""{"type":"integer"}""", "1.0000000000000000000000000000000000000001", "type-mismatch")]
    [InlineData(/*lang=json,strict*/"""{"type":"integer"}""", "1e-100000", "type-mismatch")]
    [InlineData("false", "null", "false-schema")]
    public void Evaluate_WhenCandidateViolatesSchema_ReturnsBoundedIssue(string schemaJson, string candidateJson, string code)
    {
        var result = Evaluate(schemaJson, candidateJson);

        var invalid = result.ShouldBeOfType<OutputSchemaCandidateInvalid>();
        invalid.Issues.Length.ShouldBe(1);
        invalid.Issues[0].Code.ShouldBe(code);
    }

    [Fact]
    public void Evaluate_WhenManifestWasForged_ReturnsConfigurationRejection()
    {
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"string"}""");
        var accepted = _engine.Preflight(
            new OutputSchemaPreflightRequest(schema, Limits),
            TestContext.Current.CancellationToken).ShouldBeOfType<OutputSchemaPreflightAccepted>();
        var forged = new OutputSchemaPreflightManifest(
            accepted.Manifest.Profile,
            accepted.Manifest.Dialect,
            new ContentHash("sha256:forged"),
            accepted.Manifest.Limits,
            accepted.Manifest.ObservedDepth,
            accepted.Manifest.ObservedNodes);
        var request = new OutputSchemaEvaluationRequest(schema, TestFactory.ParseJson("\"ok\""), forged, Limits, Limits, 4);

        var result = _engine.Evaluate(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputSchemaEvaluationConfigurationRejected>().Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.PreflightEvidenceMismatch);
    }

    [Fact]
    public void Evaluate_WhenCandidateContainsDuplicateObjectMember_ReturnsCandidateInvalid()
    {
        var result = Evaluate(
            /*lang=json,strict*/"""{"type":"object","additionalProperties":true}""",
            /*lang=json,strict*/"""{"same":1,"same":2}""");

        result.ShouldBeOfType<OutputSchemaCandidateInvalid>().Issues[0].Code.ShouldBe("duplicate-object-member");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Evaluate_WhenExponentHasTenThousandDigits_DecidesIntegerWithoutUnboundedArithmetic(
        bool negativeExponent,
        bool zeroSignificand)
    {
        var exponent = new string('9', 10_000);
        var candidate = $"{(zeroSignificand ? "0" : "1")}e{(negativeExponent ? "-" : string.Empty)}{exponent}";
        var largeLimits = new OutputSchemaProcessingLimits(20_000, 16, 128);
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"integer"}""");
        var manifest = _engine.Preflight(
            new OutputSchemaPreflightRequest(schema, largeLimits),
            TestContext.Current.CancellationToken).ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest;

        var result = _engine.Evaluate(
            new OutputSchemaEvaluationRequest(
                schema,
                TestFactory.ParseJson(candidate),
                manifest,
                largeLimits,
                largeLimits,
                1),
            TestContext.Current.CancellationToken);

        if (negativeExponent && !zeroSignificand)
        {
            _ = result.ShouldBeOfType<OutputSchemaCandidateInvalid>();
        }
        else
        {
            _ = result.ShouldBeOfType<OutputSchemaEvaluationPassed>();
        }
    }

    [Fact]
    public void Evaluate_WhenCandidateDepthLimitExceedsEngineSafetyLimit_ReturnsConfigurationRejection()
    {
        var schema = TestFactory.Schema("true");
        var manifest = _engine.Preflight(new OutputSchemaPreflightRequest(schema, Limits), TestContext.Current.CancellationToken)
            .ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest;
        var request = new OutputSchemaEvaluationRequest(
            schema,
            TestFactory.ParseJson("null"),
            manifest,
            Limits,
            new OutputSchemaProcessingLimits(4096, 129, 128),
            1);

        var result = _engine.Evaluate(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputSchemaEvaluationConfigurationRejected>().Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.ResourceLimitExceeded);
    }

    [Fact]
    public void Preflight_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(() => _engine.Preflight(Request("true"), cancellation.Token));
    }

    private OutputSchemaEvaluationResult Evaluate(string schemaJson, string candidateJson)
    {
        var schema = TestFactory.Schema(schemaJson);
        var manifest = _engine.Preflight(new OutputSchemaPreflightRequest(schema, Limits))
            .ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest;
        return _engine.Evaluate(
            new OutputSchemaEvaluationRequest(
                schema,
                TestFactory.ParseJson(candidateJson),
                manifest,
                Limits,
                Limits,
                maximumIssues: 1));
    }

    private static OutputSchemaPreflightRequest Request(string json) =>
        new(TestFactory.Schema(json), Limits);
}
