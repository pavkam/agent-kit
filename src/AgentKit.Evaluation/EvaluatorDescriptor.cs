// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Declares what one evaluator is, which version of its behavior this is, and what it can assess.</summary>
/// <remarks>
/// An incompatible plan fails validation before any case runs, or yields a typed unsupported outcome only where the plan
/// explicitly permits it. A descriptor with no supported criteria accepts any case; otherwise a case must carry at least one
/// supported criterion. The descriptor never grants permissions.
/// </remarks>
public sealed record EvaluatorDescriptor
{
    /// <summary>Initializes a validated descriptor.</summary>
    /// <param name="key">The stable evaluator key.</param>
    /// <param name="version">The behavior version.</param>
    /// <param name="displayName">The non-blank human-readable name.</param>
    /// <param name="supportedCriteria">The criterion kinds the evaluator assesses; empty when it accepts any case.</param>
    /// <param name="requiresFixture">Whether the evaluator can only assess a case that references a fixture.</param>
    /// <exception cref="ArgumentException">The key is blank, the name is blank, or <paramref name="supportedCriteria"/> is default, blank, or repeated.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not positive.</exception>
    public EvaluatorDescriptor(
        EvaluatorKey key,
        EvaluatorVersion version,
        string displayName,
        ImmutableArray<EvaluationCriterionKey> supportedCriteria,
        bool requiresFixture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfDefault(supportedCriteria);
        HashSet<EvaluationCriterionKey> keys = [];
        foreach (var criterion in supportedCriteria)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(criterion.Value, nameof(supportedCriteria));
            ArgumentException.ThrowIfNotEqual(keys.Add(criterion), true, nameof(supportedCriteria));
        }

        Key = key;
        Version = version;
        DisplayName = displayName;
        SupportedCriteria = supportedCriteria;
        RequiresFixture = requiresFixture;
    }

    /// <summary>Gets the stable evaluator key.</summary>
    public EvaluatorKey Key { get; }

    /// <summary>Gets the behavior version recorded with every result.</summary>
    public EvaluatorVersion Version { get; }

    /// <summary>Gets the human-readable name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the criterion kinds the evaluator assesses, or an empty array when it accepts any case.</summary>
    public ImmutableArray<EvaluationCriterionKey> SupportedCriteria { get; }

    /// <summary>Gets whether the evaluator can only assess a case that references a fixture.</summary>
    public bool RequiresFixture { get; }

    /// <summary>Reports whether the evaluator can assess a case with the given criteria and fixture.</summary>
    /// <param name="criteria">The case criteria.</param>
    /// <param name="fixture">The case fixture, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a supported criterion is present (or none are declared) and any required fixture is referenced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="criteria"/> is <see langword="null"/>.</exception>
    public bool Supports(EvaluationCriteria criteria, EvaluationFixtureReference? fixture)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (RequiresFixture && fixture is null)
        {
            return false;
        }

        if (SupportedCriteria.IsEmpty)
        {
            return true;
        }

        foreach (var key in SupportedCriteria)
        {
            if (criteria.Contains(key))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public bool Equals(EvaluatorDescriptor? other) =>
        other is not null
        && Key == other.Key
        && Version == other.Version
        && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal)
        && SupportedCriteria.SequenceEqual(other.SupportedCriteria)
        && RequiresFixture == other.RequiresFixture;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(Version);
        hash.Add(DisplayName, StringComparer.Ordinal);
        foreach (var criterion in SupportedCriteria)
        {
            hash.Add(criterion);
        }

        hash.Add(RequiresFixture);
        return hash.ToHashCode();
    }
}
