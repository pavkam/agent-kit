// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Deterministically validates the final output against the schema of a <see cref="SchemaCriterion"/>.</summary>
/// <remarks>
/// The evaluator delegates to the registered <see cref="IOutputSchemaEngine"/>: complete schema preflight first, then candidate
/// evaluation under the criterion limits. A schema the engine rejects is reported as unsupported rather than as a model failure.
/// Issues are recorded by code and path only. The evaluator is stateless and thread-safe.
/// </remarks>
public sealed class SchemaEvaluator: IEvaluator
{
    private readonly IOutputSchemaEngine _engine;

    /// <summary>Gets the documented evaluator key, <c>schema</c>.</summary>
    public static EvaluatorKey Key { get; } = new("schema");

    /// <summary>Initializes the evaluator over one schema engine.</summary>
    /// <param name="engine">The engine that preflights schemas and evaluates candidates.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <see langword="null"/>.</exception>
    public SchemaEvaluator(IOutputSchemaEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc/>
    public EvaluatorDescriptor Descriptor { get; } = new(Key, new EvaluatorVersion(1), "Output schema", [SchemaCriterion.CriterionKey], requiresFixture: false);

    /// <inheritdoc/>
    public ValueTask<EvaluationOutcome> EvaluateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Evaluate(context, cancellationToken));
    }

    private EvaluationOutcome Evaluate(EvaluationContext context, CancellationToken cancellationToken)
    {
        if (context.Case.Criteria.Find<SchemaCriterion>() is not { } criterion)
        {
            return new EvaluationUnsupported("The case declares no schema criterion.");
        }

        if (context.Finished is null)
        {
            return new EvaluationSkipped("The run produced no result to validate.");
        }

        var profile = _engine.Profile;
        ImmutableArray<EvaluationEvidence> evidence =
        [
            new("schema.name", criterion.Schema.Name),
            new("schema.version", criterion.Schema.Version.ToString()),
            new("engine.profile", $"{profile.Id}@{profile.Version}"),
        ];
        if (_engine.Preflight(new OutputSchemaPreflightRequest(criterion.Schema, criterion.Limits), cancellationToken) is not OutputSchemaPreflightAccepted accepted)
        {
            return new EvaluationUnsupported("The schema engine cannot support the criterion schema.", evidence);
        }

        if (!EvaluationOutputReader.TryReadJson(context, out var candidate))
        {
            return new EvaluationFailed(EvaluationScore.Certain(false), "The output is not a JSON value.", evidence);
        }

        var result = _engine.Evaluate(
            new OutputSchemaEvaluationRequest(criterion.Schema, candidate, accepted.Manifest, criterion.Limits, criterion.Limits, criterion.MaximumIssues),
            cancellationToken);
        switch (result)
        {
            case OutputSchemaEvaluationPassed:
                return new EvaluationPassed(EvaluationScore.Certain(true), "The output satisfies the schema.", evidence);
            case OutputSchemaCandidateInvalid invalid:
                var issues = invalid.Issues.Select(static (issue, index) => new EvaluationEvidence($"issue.{index}", $"{issue.Code} at {issue.Path ?? "$"}"));
                return new EvaluationFailed(
                    EvaluationScore.Certain(false),
                    $"The output violates the schema with {invalid.Issues.Length} issue(s).",
                    [.. evidence, .. issues]);
            default:
                return new EvaluationUnsupported("The schema engine rejected the evaluation configuration.", evidence);
        }
    }
}
