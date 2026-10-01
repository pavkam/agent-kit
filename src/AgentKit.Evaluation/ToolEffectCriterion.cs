// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Expects specific tool effects: calls that must happen and tools that must never be called.</summary>
public sealed record ToolEffectCriterion: EvaluationCriterion
{
    /// <summary>Gets the criterion key, <c>tool-effect</c>.</summary>
    public static EvaluationCriterionKey CriterionKey { get; } = new("tool-effect");

    /// <summary>Initializes a validated criterion.</summary>
    /// <param name="required">The expected calls; may be empty when <paramref name="forbidden"/> is not.</param>
    /// <param name="forbidden">The aliases or canonical identities of tools that must not be called; may be empty when <paramref name="required"/> is not.</param>
    /// <exception cref="ArgumentException">An array is default or empty together, contains null or a blank tool, repeats a forbidden tool, or a tool is both required and forbidden.</exception>
    public ToolEffectCriterion(ImmutableArray<ExpectedToolCall> required, ImmutableArray<string> forbidden)
        : base(CriterionKey)
    {
        ArgumentException.ThrowIfDefault(required);
        ArgumentException.ThrowIfContainsNull(required);
        ArgumentException.ThrowIfDefault(forbidden);
        if (required.IsEmpty && forbidden.IsEmpty)
        {
            throw new ArgumentException("At least one required or forbidden tool effect must be declared.", nameof(required));
        }

        HashSet<string> forbiddenTools = new(StringComparer.Ordinal);
        foreach (var tool in forbidden)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tool, nameof(forbidden));
            ArgumentException.ThrowIfNotEqual(forbiddenTools.Add(tool), true, nameof(forbidden));
        }

        foreach (var expectation in required)
        {
            ArgumentException.ThrowIfNotEqual(forbiddenTools.Contains(expectation.Tool), false, nameof(forbidden));
        }

        Required = required;
        Forbidden = forbidden;
    }

    /// <summary>Gets the expected calls.</summary>
    public ImmutableArray<ExpectedToolCall> Required { get; }

    /// <summary>Gets the tools that must not be called.</summary>
    public ImmutableArray<string> Forbidden { get; }

    /// <inheritdoc/>
    public bool Equals(ToolEffectCriterion? other) =>
        other is not null && Required.SequenceEqual(other.Required) && Forbidden.SequenceEqual(other.Forbidden);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        foreach (var expectation in Required)
        {
            hash.Add(expectation);
        }

        foreach (var tool in Forbidden)
        {
            hash.Add(tool, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
