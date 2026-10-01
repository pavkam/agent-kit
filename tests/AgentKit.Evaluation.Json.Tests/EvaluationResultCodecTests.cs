// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json.Tests;

public sealed class EvaluationResultCodecTests
{
    private static readonly JsonSerializerOptions _options = JsonStoreSerialization.CreateCanonicalOptions();

    [Fact]
    public void Encode_ThenDecode_WhenEveryVariantIsPopulated_RestoresTheResultExactly()
    {
        var result = EvaluationResultConformanceData.Complete(EvaluationResultConformanceData.NewRun());

        var bytes = EvaluationResultCodec.Encode(result, _options, 1_048_576);

        EvaluationResultCodec.Decode(bytes, _options).ShouldBe(result);
        EvaluationResultCodec.Encode(EvaluationResultCodec.Decode(bytes, _options), _options, 1_048_576).ShouldBe(bytes);
    }

    [Fact]
    public void Encode_WhenTheResultIsSparse_RoundTripsAbsentOptionalMembers()
    {
        var result = EvaluationResultConformanceData.Sparse(EvaluationResultConformanceData.NewRun());

        EvaluationResultCodec.Decode(EvaluationResultCodec.Encode(result, _options, 1_048_576), _options).ShouldBe(result);
    }

    [Fact]
    public void Encode_WhenArgumentsAreInvalid_ThrowsBeforeEncoding()
    {
        var result = EvaluationResultConformanceData.Sparse(EvaluationResultConformanceData.NewRun());

        Should.Throw<ArgumentNullException>(() => EvaluationResultCodec.Encode(null!, _options, 10)).ParamName.ShouldBe("result");
        Should.Throw<ArgumentNullException>(() => EvaluationResultCodec.Encode(result, null!, 10)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentOutOfRangeException>(() => EvaluationResultCodec.Encode(result, _options, 0)).ParamName.ShouldBe("maximumBytes");
    }

    [Fact]
    public void Encode_WhenTheBoundIsTooSmall_ThrowsInvalidDataException() =>
        Should.Throw<InvalidDataException>(() => EvaluationResultCodec.Encode(EvaluationResultConformanceData.Complete(EvaluationResultConformanceData.NewRun()), _options, 10));

    [Fact]
    public void Decode_WhenThePayloadHasAnUnmappedMemberOrNull_FailsClosed()
    {
        _ = Should.Throw<JsonException>(() => EvaluationResultCodec.Decode(/*lang=json,strict*/ """{"unknown":1}"""u8, _options));
        _ = Should.Throw<InvalidDataException>(() => EvaluationResultCodec.Decode("null"u8, _options));
    }

    [Fact]
    public void Decode_WhenAnOutcomeVariantIsUnknownOrAnErrorTypeIsMissing_ThrowsInvalidDataException()
    {
        var document = EvaluationResultDocument.FromDomain(EvaluationResultConformanceData.Complete(EvaluationResultConformanceData.NewRun()));
        var unknown = document with { Evaluators = [document.Evaluators[0] with { Outcome = document.Evaluators[0].Outcome with { Kind = "mystery" } }] };
        var faulted = document.Evaluators.Single(static e => e.Outcome.Kind == "evaluator_failed");
        var missing = document with { Evaluators = [faulted with { Outcome = faulted.Outcome with { ErrorType = null } }] };

        _ = Should.Throw<InvalidDataException>(unknown.ToDomain);
        _ = Should.Throw<InvalidDataException>(missing.ToDomain);
    }

    [Fact]
    public void ToDomain_WhenADocumentViolatesDomainRules_ThrowsInsteadOfProducingAnInvalidResult()
    {
        var document = EvaluationResultDocument.FromDomain(EvaluationResultConformanceData.Sparse(EvaluationResultConformanceData.NewRun()));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => (document with { Repetition = 0 }).ToDomain());
        _ = Should.Throw<ArgumentException>(() => (document with { CaseId = " " }).ToDomain());
    }

    [Fact]
    public void Probe_WhenCreated_CoversEveryOutcomeVariantAndRoundTrips()
    {
        var probe = EvaluationResultProbe.Create();

        probe.Evaluators.Select(static e => e.Outcome.Name).ShouldBe(["passed", "failed", "inconclusive", "skipped", "cancelled", "unsupported", "evaluator_failed"]);
        EvaluationResultCodec.Decode(EvaluationResultCodec.Encode(probe, _options, 1_048_576), _options).ShouldBe(probe);
    }

    [Fact]
    public void FromDomain_WhenAnArgumentIsNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() => EvaluationResultDocument.FromDomain(null!));
        _ = Should.Throw<ArgumentNullException>(() => EvaluationOutcomeDocument.FromDomain(null!));
        _ = Should.Throw<ArgumentNullException>(() => EvaluatorResultDocument.FromDomain(null!));
        _ = Should.Throw<ArgumentNullException>(() => EvaluationManifestDocument.FromDomain(null!));
        _ = Should.Throw<ArgumentNullException>(() => EvaluationModelUseDocument.FromDomain(null!));
        _ = Should.Throw<ArgumentNullException>(() => EvaluationUsageDocument.FromDomain(null!));
        _ = Should.Throw<ArgumentNullException>(() => EvaluationRunRecordDocument.FromDomain(null!));
        _ = Should.Throw<ArgumentNullException>(() => EvaluationFixtureDocument.FromDomain(null!));
        _ = Should.Throw<ArgumentNullException>(() => EvaluationDiagnosticDocument.FromDomain(null!));
        _ = Should.Throw<ArgumentNullException>(() => EvaluationScoreDocument.FromDomain(null!));
        _ = Should.Throw<ArgumentNullException>(() => EvaluationEvidenceDocument.FromDomain(null!));
    }
}
