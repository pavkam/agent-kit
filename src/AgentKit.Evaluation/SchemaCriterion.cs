// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Expects the final output to validate against one JSON schema.</summary>
/// <remarks>The schema is checked by the registered <see cref="IOutputSchemaEngine"/> under explicit processing limits, so a schema the engine cannot support is a configuration failure rather than a model failure.</remarks>
public sealed record SchemaCriterion: EvaluationCriterion
{
    /// <summary>Gets the criterion key, <c>schema</c>.</summary>
    public static EvaluationCriterionKey CriterionKey { get; } = new("schema");

    /// <summary>Gets the limits used when none are supplied: 256 KiB, depth 64, and 100000 nodes.</summary>
    public static OutputSchemaProcessingLimits DefaultLimits { get; } = new(262_144, 64, 100_000);

    /// <summary>Initializes a validated criterion.</summary>
    /// <param name="schema">The schema the output must satisfy.</param>
    /// <param name="limits">The bounds on schema and candidate size, depth, and node count, or <see langword="null"/> for <see cref="DefaultLimits"/>.</param>
    /// <param name="maximumIssues">The positive cap on reported issues.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumIssues"/> is not positive.</exception>
    public SchemaCriterion(JsonSchemaDocument schema, OutputSchemaProcessingLimits? limits = null, int maximumIssues = 16)
        : base(CriterionKey)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIssues);
        Schema = schema;
        Limits = limits ?? DefaultLimits;
        MaximumIssues = maximumIssues;
    }

    /// <summary>Gets the schema the output must satisfy.</summary>
    public JsonSchemaDocument Schema { get; }

    /// <summary>Gets the processing bounds applied to both the schema and the candidate.</summary>
    public OutputSchemaProcessingLimits Limits { get; }

    /// <summary>Gets the cap on reported issues.</summary>
    public int MaximumIssues { get; }
}
