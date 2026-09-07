// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

internal static class TestFactory
{
    public static ModelResponse TextResponse(string text) =>
        Response([new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)]);

    public static ModelResponse StructuredResponse(JsonElement value, JsonSchemaReference? schema = null) =>
        Response([new StructuredDataPart(value, schema, ExtensionData.Empty)]);

    public static ModelResponse Response(ImmutableArray<ContentPart> parts) =>
        new(
            new ModelRequestId(Guid.NewGuid()),
            new ProviderResponseIdentity(
                new ProviderId("test-provider"),
                null,
                new ApiFamilyId("test-api"),
                new ModelId("test-model"),
                new ModelId("test-model"),
                null,
                null,
                null),
            parts,
            NormalizedStopReason.Completed,
            ModelUsage.Empty,
            ExtensionData.Empty);

    public static JsonSchemaDocument Schema(string json) =>
        new("test-schema", new SchemaVersion("1.0"), JsonDocument.Parse(json).RootElement);

    public static OutputDefinition Definition(
        OutputMode mode = OutputMode.Text,
        JsonSchemaDocument? schema = null,
        Type? runtimeType = null,
        ImmutableArray<OutputValidatorReference>? validators = null,
        OutputValidationPolicy? validationPolicy = null,
        OutputRetryPolicy? retryPolicy = null,
        string id = "test-definition") =>
        new(
            new OutputDefinitionId(id),
            new OutputDefinitionVersion("1.0"),
            "Test Definition",
            mode,
            schema,
            runtimeType,
            [],
            validators ?? [],
            validationPolicy ?? OutputValidationPolicy.RejectOnFirstFailure,
            retryPolicy ?? OutputRetryPolicy.None,
            OutputEndStrategy.Graceful);

    public static OutputProcessingRequest ProcessingRequest(
        OutputDefinition definition, ModelResponse response, int attempt = 1) =>
        new(definition, response, attempt);

    public static JsonElement ParseJson(string json) => JsonDocument.Parse(json).RootElement;
}
