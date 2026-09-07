// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Supplies an owned candidate and captured preflight evidence for bounded local schema evaluation.</summary>
public sealed record OutputSchemaEvaluationRequest
{
    /// <summary>Initializes an evaluation request.</summary>
    /// <param name="schema">The non-null canonical schema retained by the request.</param>
    /// <param name="candidate">The defined candidate JSON to clone and own.</param>
    /// <param name="manifest">The non-null captured preflight evidence to revalidate.</param>
    /// <param name="schemaLimits">The non-null schema-processing limits.</param>
    /// <param name="candidateLimits">The non-null candidate-processing limits.</param>
    /// <param name="maximumIssues">The positive maximum issue count.</param>
    /// <exception cref="ArgumentNullException">A supplied reference is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="candidate"/> is undefined or <paramref name="maximumIssues"/> is not positive.</exception>
    public OutputSchemaEvaluationRequest(JsonSchemaDocument schema, JsonElement candidate, OutputSchemaPreflightManifest manifest, OutputSchemaProcessingLimits schemaLimits, OutputSchemaProcessingLimits candidateLimits, int maximumIssues)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentOutOfRangeException.ThrowIfEqual(candidate.ValueKind, JsonValueKind.Undefined, nameof(candidate));
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(schemaLimits);
        ArgumentNullException.ThrowIfNull(candidateLimits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIssues);
        var candidateClone = candidate.Clone();

        Schema = schema;
        Candidate = candidateClone;
        Manifest = manifest;
        SchemaLimits = schemaLimits;
        CandidateLimits = candidateLimits;
        MaximumIssues = maximumIssues;
    }
    /// <summary>Gets canonical schema.</summary><value>A retained schema value.</value>
    public JsonSchemaDocument Schema { get; }
    /// <summary>Gets an owned candidate clone.</summary><value>A defined detached JSON element.</value>
    public JsonElement Candidate { get; }
    /// <summary>Gets preflight evidence that evaluation must revalidate.</summary><value>Evidence, not authorization to skip checks.</value>
    public OutputSchemaPreflightManifest Manifest { get; }
    /// <summary>Gets schema limits.</summary><value>Non-null bounded limits.</value>
    public OutputSchemaProcessingLimits SchemaLimits { get; }
    /// <summary>Gets candidate limits.</summary><value>Non-null bounded limits.</value>
    public OutputSchemaProcessingLimits CandidateLimits { get; }
    /// <summary>Gets maximum diagnostics.</summary><value>A positive count.</value>
    public int MaximumIssues { get; }
}
