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

    private static OutputSchemaEngineProfile Profile(ImmutableArray<JsonSchemaDialectId>? dialects = null, ImmutableArray<string>? assertions = null, ImmutableArray<string>? annotations = null) => new(new OutputSchemaProfileId("test"), new OutputSchemaProfileVersion(1), _dialect, dialects ?? [_dialect], assertions ?? ["type"], annotations ?? ["title"]);
    private static OutputSchemaProcessingLimits Limits() => new(128, 2, 2);
    private static OutputSchemaPreflightManifest Manifest() => new(Profile(), _dialect, new ContentHash("hash"), Limits(), 1, 1);
}
