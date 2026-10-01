// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Expects safety properties of the produced text: forbidden content never appears and a required marker does.</summary>
/// <remarks>
/// Forbidden patterns run as non-backtracking regular expressions under a one-second match limit, so a hostile pattern or input
/// cannot stall evaluation. Results record only which rule was violated, never the matched content, so a canary or secret never
/// reaches a result store.
/// </remarks>
public sealed record SafetyCriterion: EvaluationCriterion
{
    /// <summary>Gets the criterion key, <c>safety</c>.</summary>
    public static EvaluationCriterionKey CriterionKey { get; } = new("safety");

    /// <summary>Initializes a validated criterion.</summary>
    /// <param name="forbiddenSubstrings">Text that must not appear; may be empty.</param>
    /// <param name="forbiddenPatterns">Regular expressions that must not match; may be empty.</param>
    /// <param name="requiredAnyOf">Markers of which at least one must appear, such as a refusal phrase; may be empty.</param>
    /// <param name="ignoreCase">Whether substring and marker matching ignores case.</param>
    /// <param name="includeToolArguments">Whether the raw arguments of tool calls are scanned together with the text.</param>
    /// <exception cref="ArgumentException">No rule is declared, an array is default or contains an empty value, or a pattern is not a valid regular expression.</exception>
    public SafetyCriterion(
        ImmutableArray<string> forbiddenSubstrings,
        ImmutableArray<string> forbiddenPatterns,
        ImmutableArray<string> requiredAnyOf,
        bool ignoreCase = false,
        bool includeToolArguments = true)
        : base(CriterionKey)
    {
        ArgumentException.ThrowIfDefault(forbiddenSubstrings);
        ArgumentException.ThrowIfDefault(forbiddenPatterns);
        ArgumentException.ThrowIfDefault(requiredAnyOf);
        if (forbiddenSubstrings.IsEmpty && forbiddenPatterns.IsEmpty && requiredAnyOf.IsEmpty)
        {
            throw new ArgumentException("At least one safety rule must be declared.", nameof(forbiddenSubstrings));
        }

        foreach (var value in forbiddenSubstrings)
        {
            ArgumentException.ThrowIfNullOrEmpty(value, nameof(forbiddenSubstrings));
        }

        foreach (var marker in requiredAnyOf)
        {
            ArgumentException.ThrowIfNullOrEmpty(marker, nameof(requiredAnyOf));
        }

        foreach (var pattern in forbiddenPatterns)
        {
            ArgumentException.ThrowIfNullOrEmpty(pattern, nameof(forbiddenPatterns));
            try
            {
                _ = new System.Text.RegularExpressions.Regex(
                    pattern, SafetyEvaluator.PatternOptions(ignoreCase), SafetyEvaluator.MatchTimeout);
            }
            catch (ArgumentException exception) when (exception is not ArgumentNullException)
            {
                throw new ArgumentException("A forbidden pattern is not a valid non-backtracking regular expression.", nameof(forbiddenPatterns), exception);
            }
            catch (NotSupportedException exception)
            {
                throw new ArgumentException("A forbidden pattern uses a construct the non-backtracking engine does not support.", nameof(forbiddenPatterns), exception);
            }
        }

        ForbiddenSubstrings = forbiddenSubstrings;
        ForbiddenPatterns = forbiddenPatterns;
        RequiredAnyOf = requiredAnyOf;
        IgnoreCase = ignoreCase;
        IncludeToolArguments = includeToolArguments;
    }

    /// <summary>Gets the text that must not appear.</summary>
    public ImmutableArray<string> ForbiddenSubstrings { get; }

    /// <summary>Gets the regular expressions that must not match.</summary>
    public ImmutableArray<string> ForbiddenPatterns { get; }

    /// <summary>Gets the markers of which at least one must appear.</summary>
    public ImmutableArray<string> RequiredAnyOf { get; }

    /// <summary>Gets whether substring and marker matching ignores case.</summary>
    public bool IgnoreCase { get; }

    /// <summary>Gets whether the raw arguments of tool calls are scanned together with the text.</summary>
    public bool IncludeToolArguments { get; }

    /// <inheritdoc/>
    public bool Equals(SafetyCriterion? other) =>
        other is not null
        && ForbiddenSubstrings.SequenceEqual(other.ForbiddenSubstrings)
        && ForbiddenPatterns.SequenceEqual(other.ForbiddenPatterns)
        && RequiredAnyOf.SequenceEqual(other.RequiredAnyOf)
        && IgnoreCase == other.IgnoreCase
        && IncludeToolArguments == other.IncludeToolArguments;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        foreach (var value in ForbiddenSubstrings)
        {
            hash.Add(value, StringComparer.Ordinal);
        }

        foreach (var value in ForbiddenPatterns)
        {
            hash.Add(value, StringComparer.Ordinal);
        }

        foreach (var value in RequiredAnyOf)
        {
            hash.Add(value, StringComparer.Ordinal);
        }

        hash.Add(IgnoreCase);
        hash.Add(IncludeToolArguments);
        return hash.ToHashCode();
    }
}
