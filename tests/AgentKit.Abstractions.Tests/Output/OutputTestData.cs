// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

internal static class OutputTestData
{
    public static readonly JsonSchemaDialectId Dialect = new("urn:test:dialect");

    public static OutputDefinition Definition(
        OutputDefinitionId? id = null,
        OutputDefinitionVersion? version = null,
        ImmutableArray<OutputAlternative>? alternatives = null,
        ImmutableArray<OutputValidatorReference>? validators = null) => new(
        id ?? new OutputDefinitionId("output"),
        version ?? new OutputDefinitionVersion("1"),
        "Output",
        OutputMode.Text,
        schema: null,
        runtimeType: null,
        alternatives ?? [],
        validators ?? [],
        OutputValidationPolicy.RejectOnFirstFailure,
        OutputRetryPolicy.None,
        OutputEndStrategy.Graceful);

    public static ModelResponse Response() => new(
        new ModelRequestId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new ProviderResponseIdentity(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null),
        [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)],
        NormalizedStopReason.Completed,
        ModelUsage.NotReported,
        ExtensionData.Empty);

    public static OutputValidationIssue Issue(string code = "code", string message = "message", string? path = null) =>
        new(code, message, path);

    public static OutputValidationFailure Failure() =>
        new(OutputValidationFailureKind.ValidatorFailed, "Validation failed.", [Issue()]);

    public static ValidatedOutput Candidate() => new(OutputMode.Text, "text", null, null);

    public static OutputValidationManifest Manifest() =>
        new(new OutputDefinitionId("output"), OutputMode.Text, 1, []);

    public static OutputSchemaEngineProfile Profile(
        ImmutableArray<JsonSchemaDialectId>? dialects = null,
        ImmutableArray<string>? assertions = null,
        ImmutableArray<string>? annotations = null) => new(
        new OutputSchemaProfileId("test"),
        new OutputSchemaProfileVersion(1),
        Dialect,
        dialects ?? [Dialect],
        assertions ?? ["type"],
        annotations ?? ["title"]);

    public static OutputSchemaProcessingLimits Limits() => new(128, 2, 2);

    public static OutputSchemaPreflightManifest PreflightManifest() =>
        new(Profile(), Dialect, new ContentHash("hash"), Limits(), 1, 1);

    public static OutputSchemaConfigurationFailure ConfigurationFailure() =>
        new(OutputSchemaConfigurationFailureKind.MalformedSchema, "Malformed schema.", [Issue()]);
}
