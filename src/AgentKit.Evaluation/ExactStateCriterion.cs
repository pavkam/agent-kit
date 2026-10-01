// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Expects exact final state: the run outcome, the output text, the output JSON, and the count of new messages.</summary>
/// <remarks>Every declared expectation must hold; an expectation left <see langword="null"/> is not checked. At least one expectation is required.</remarks>
public sealed record ExactStateCriterion: EvaluationCriterion
{
    /// <summary>Gets the criterion key, <c>exact-state</c>.</summary>
    public static EvaluationCriterionKey CriterionKey { get; } = new("exact-state");

    /// <summary>Initializes a validated criterion.</summary>
    /// <param name="outcome">The expected terminal run outcome, or <see langword="null"/> to skip the check.</param>
    /// <param name="text">The expected output text, or <see langword="null"/> to skip the check.</param>
    /// <param name="textComparison">How <paramref name="text"/> is compared.</param>
    /// <param name="json">The expected output JSON, compared structurally, or <see langword="null"/> to skip the check.</param>
    /// <param name="newMessageCount">The expected number of new messages the run committed, or <see langword="null"/> to skip the check.</param>
    /// <exception cref="ArgumentException">No expectation is declared.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> or <paramref name="textComparison"/> is undefined, or <paramref name="newMessageCount"/> is negative.</exception>
    public ExactStateCriterion(
        ExpectedRunOutcome? outcome = null,
        string? text = null,
        ExactTextComparison textComparison = ExactTextComparison.Ordinal,
        JsonElement? json = null,
        int? newMessageCount = null)
        : base(CriterionKey)
    {
        if (outcome is { } expectedOutcome)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(expectedOutcome, nameof(outcome));
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(textComparison);
        if (json is { } expectedJson)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(expectedJson.ValueKind, JsonValueKind.Undefined, nameof(json));
        }

        if (newMessageCount is { } count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(newMessageCount));
        }

        if (outcome is null && text is null && json is null && newMessageCount is null)
        {
            throw new ArgumentException("At least one exact-state expectation must be declared.", nameof(outcome));
        }

        Outcome = outcome;
        Text = text;
        TextComparison = textComparison;
        Json = json?.Clone();
        NewMessageCount = newMessageCount;
    }

    /// <summary>Gets the expected terminal run outcome, or <see langword="null"/>.</summary>
    public ExpectedRunOutcome? Outcome { get; }

    /// <summary>Gets the expected output text, or <see langword="null"/>.</summary>
    public string? Text { get; }

    /// <summary>Gets how <see cref="Text"/> is compared.</summary>
    public ExactTextComparison TextComparison { get; }

    /// <summary>Gets the expected output JSON, or <see langword="null"/>.</summary>
    public JsonElement? Json { get; }

    /// <summary>Gets the expected number of new messages, or <see langword="null"/>.</summary>
    public int? NewMessageCount { get; }

    /// <inheritdoc/>
    public bool Equals(ExactStateCriterion? other) =>
        other is not null
        && Outcome == other.Outcome
        && string.Equals(Text, other.Text, StringComparison.Ordinal)
        && TextComparison == other.TextComparison
        && (Json is null
            ? other.Json is null
            : other.Json is { } otherJson && JsonElement.DeepEquals(Json.Value, otherJson))
        && NewMessageCount == other.NewMessageCount;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Key, Outcome, Text, TextComparison, Json?.GetRawText(), NewMessageCount);
}
