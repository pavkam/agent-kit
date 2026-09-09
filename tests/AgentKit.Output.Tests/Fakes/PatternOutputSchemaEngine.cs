// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests.Fakes;

using System.Security.Cryptography;

/// <summary>Implements a deterministic test-only lowercase-pattern schema profile.</summary>
internal sealed class PatternOutputSchemaEngine: IOutputSchemaEngine
{
    private static readonly JsonSchemaDialectId _dialect = new("urn:agentkit:test:lowercase-pattern:v1");

    /// <inheritdoc/>
    public OutputSchemaEngineProfile Profile { get; } = new(
        new OutputSchemaProfileId("test-lowercase-pattern"),
        new OutputSchemaProfileVersion(1),
        _dialect,
        [_dialect],
        ["pattern"],
        []);

    /// <inheritdoc/>
    public OutputSchemaPreflightResult Preflight(
        OutputSchemaPreflightRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Schema.Schema.ValueKind != JsonValueKind.Object
            || !request.Schema.Schema.TryGetProperty("pattern", out var pattern)
            || pattern.ValueKind != JsonValueKind.String
            || pattern.GetString() != "^[a-z]+$")
        {
            return new OutputSchemaPreflightRejected(
                new OutputSchemaConfigurationFailure(
                    OutputSchemaConfigurationFailureKind.UnsupportedVocabulary,
                    "The test schema must declare the lowercase pattern.",
                    []));
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(request.Schema.Schema);
        if (bytes.Length > request.Limits.MaximumUtf8Bytes)
        {
            return new OutputSchemaPreflightRejected(
                new OutputSchemaConfigurationFailure(
                    OutputSchemaConfigurationFailureKind.ResourceLimitExceeded,
                    "The test schema exceeds its byte limit.",
                    []));
        }

        var fingerprint = new ContentHash("sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return new OutputSchemaPreflightAccepted(
            new OutputSchemaPreflightManifest(Profile, _dialect, fingerprint, request.Limits, 2, 2));
    }

    /// <inheritdoc/>
    public OutputSchemaEvaluationResult Evaluate(
        OutputSchemaEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var current = Preflight(new OutputSchemaPreflightRequest(request.Schema, request.SchemaLimits), cancellationToken);
        return current is not OutputSchemaPreflightAccepted accepted || !accepted.Manifest.Equals(request.Manifest)
            ? new OutputSchemaEvaluationConfigurationRejected(
                new OutputSchemaConfigurationFailure(
                    OutputSchemaConfigurationFailureKind.PreflightEvidenceMismatch,
                    "The test schema evidence does not match.",
                    []))
            : request.Candidate.ValueKind == JsonValueKind.String
            && request.Candidate.GetString() is { Length: > 0 } value
            && value.All(static character => character is >= 'a' and <= 'z')
                ? OutputSchemaEvaluationPassed.Instance
                : new OutputSchemaCandidateInvalid(
                    [new OutputValidationIssue("pattern", "The candidate must contain lowercase ASCII letters.", "$")]);
    }
}
