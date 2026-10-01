// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Defines one versioned evaluation case: the agent, who runs it, what it is asked, and how it is judged.</summary>
/// <remarks>A case names an <see cref="AgentId"/> from the one definition catalog of the engine; it never mutates a current agent or substitutes a second catalog.</remarks>
public sealed record EvaluationCase
{
    /// <summary>Initializes a validated case.</summary>
    /// <param name="id">The case identity, unique within its plan.</param>
    /// <param name="agentId">The agent definition to run.</param>
    /// <param name="execution">The identity and expected session profile.</param>
    /// <param name="input">The input admitted to the case session.</param>
    /// <param name="runOptions">The run overrides, which may only narrow the limits of the definition.</param>
    /// <param name="evaluators">The evaluators to run over the result, in order; an empty array only records the run.</param>
    /// <param name="criteria">The expected criteria evaluators assess.</param>
    /// <param name="fixture">The fixture the case ran against, or <see langword="null"/> when it needs none.</param>
    /// <exception cref="ArgumentNullException">A required reference is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is default or blank, or <paramref name="evaluators"/> is the default array, contains null, or repeats an evaluator key.</exception>
    public EvaluationCase(
        EvaluationCaseId id,
        AgentId agentId,
        EvaluationCaseExecution execution,
        AgentInput input,
        AgentRunOptions runOptions,
        ImmutableArray<EvaluatorReference> evaluators,
        EvaluationCriteria criteria,
        EvaluationFixtureReference? fixture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(runOptions);
        ArgumentException.ThrowIfDefault(evaluators);
        ArgumentException.ThrowIfContainsNull(evaluators);
        ArgumentNullException.ThrowIfNull(criteria);
        HashSet<EvaluatorKey> keys = [];
        foreach (var evaluator in evaluators)
        {
            ArgumentException.ThrowIfNotEqual(keys.Add(evaluator.Key), true, nameof(evaluators));
        }

        Id = id;
        AgentId = agentId;
        Execution = execution;
        Input = input;
        RunOptions = runOptions;
        Evaluators = evaluators;
        Criteria = criteria;
        Fixture = fixture;
    }

    /// <summary>Gets the case identity.</summary>
    public EvaluationCaseId Id { get; }

    /// <summary>Gets the agent definition to run.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the identity and expected session profile.</summary>
    public EvaluationCaseExecution Execution { get; }

    /// <summary>Gets the input admitted to the case session.</summary>
    public AgentInput Input { get; }

    /// <summary>Gets the run overrides.</summary>
    public AgentRunOptions RunOptions { get; }

    /// <summary>Gets the evaluators to run over the result, in order.</summary>
    public ImmutableArray<EvaluatorReference> Evaluators { get; }

    /// <summary>Gets the expected criteria evaluators assess.</summary>
    public EvaluationCriteria Criteria { get; }

    /// <summary>Gets the fixture the case ran against, or <see langword="null"/>.</summary>
    public EvaluationFixtureReference? Fixture { get; }

    /// <inheritdoc/>
    public bool Equals(EvaluationCase? other) =>
        other is not null
        && Id == other.Id
        && AgentId == other.AgentId
        && Execution == other.Execution
        && Input == other.Input
        && RunOptions == other.RunOptions
        && Evaluators.SequenceEqual(other.Evaluators)
        && Criteria == other.Criteria
        && Fixture == other.Fixture;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(AgentId);
        hash.Add(Execution);
        hash.Add(Input);
        hash.Add(RunOptions);
        foreach (var evaluator in Evaluators)
        {
            hash.Add(evaluator);
        }

        hash.Add(Criteria);
        hash.Add(Fixture);
        return hash.ToHashCode();
    }
}
