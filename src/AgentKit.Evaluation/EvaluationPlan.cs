// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Defines one versioned, immutable evaluation dataset and how it is executed and recorded.</summary>
public sealed record EvaluationPlan
{
    /// <summary>Initializes a validated plan.</summary>
    /// <param name="id">The stable plan identity.</param>
    /// <param name="version">The plan version.</param>
    /// <param name="cases">The cases in plan order, which fixes result order; at least one is required.</param>
    /// <param name="execution">The execution policy.</param>
    /// <param name="recording">The recording policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="execution"/> or <paramref name="recording"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An identity or version is default, or <paramref name="cases"/> is default, empty, contains null, or repeats a case identity.</exception>
    public EvaluationPlan(
        EvaluationPlanId id,
        EvaluationPlanVersion version,
        ImmutableArray<EvaluationCase> cases,
        EvaluationExecutionPolicy execution,
        EvaluationRecordingPolicy recording)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        ArgumentException.ThrowIfDefaultOrEmpty(cases);
        ArgumentException.ThrowIfContainsNull(cases);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(recording);
        HashSet<EvaluationCaseId> ids = [];
        foreach (var evaluationCase in cases)
        {
            ArgumentException.ThrowIfNotEqual(ids.Add(evaluationCase.Id), true, nameof(cases));
        }

        Id = id;
        Version = version;
        Cases = cases;
        Execution = execution;
        Recording = recording;
    }

    /// <summary>Gets the stable plan identity.</summary>
    public EvaluationPlanId Id { get; }

    /// <summary>Gets the plan version.</summary>
    public EvaluationPlanVersion Version { get; }

    /// <summary>Gets the cases in plan order.</summary>
    public ImmutableArray<EvaluationCase> Cases { get; }

    /// <summary>Gets the execution policy.</summary>
    public EvaluationExecutionPolicy Execution { get; }

    /// <summary>Gets the recording policy.</summary>
    public EvaluationRecordingPolicy Recording { get; }

    /// <inheritdoc/>
    public bool Equals(EvaluationPlan? other) =>
        other is not null
        && Id == other.Id
        && Version == other.Version
        && Cases.SequenceEqual(other.Cases)
        && Execution == other.Execution
        && Recording == other.Recording;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Version);
        foreach (var evaluationCase in Cases)
        {
            hash.Add(evaluationCase);
        }

        hash.Add(Execution);
        hash.Add(Recording);
        return hash.ToHashCode();
    }
}
