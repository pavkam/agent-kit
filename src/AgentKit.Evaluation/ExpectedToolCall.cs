// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Expects calls to one tool, with a count range, optional argument subset, and an optional success requirement.</summary>
/// <remarks>The tool is matched by the alias the model used or by the canonical tool identity. An argument subset matches when every expected object property is present with an equal value; arrays and scalars compare exactly.</remarks>
public sealed record ExpectedToolCall
{
    /// <summary>Initializes a validated expectation.</summary>
    /// <param name="tool">The non-blank tool alias or canonical identity.</param>
    /// <param name="minimumCalls">The non-negative least number of matching calls.</param>
    /// <param name="maximumCalls">The greatest number of matching calls, or <see langword="null"/> for no upper bound.</param>
    /// <param name="requireSuccess">Whether at least <paramref name="minimumCalls"/> matching calls must have terminated successfully.</param>
    /// <param name="argumentsSubset">The arguments a call must contain, or <see langword="null"/> to match any arguments.</param>
    /// <exception cref="ArgumentException"><paramref name="tool"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumCalls"/> is negative, or <paramref name="maximumCalls"/> is below the minimum.</exception>
    public ExpectedToolCall(string tool, int minimumCalls = 1, int? maximumCalls = null, bool requireSuccess = true, JsonElement? argumentsSubset = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumCalls);
        if (maximumCalls is { } maximum)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximum, minimumCalls, nameof(maximumCalls));
        }

        if (argumentsSubset is { } subset)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(subset.ValueKind, JsonValueKind.Undefined, nameof(argumentsSubset));
        }

        Tool = tool;
        MinimumCalls = minimumCalls;
        MaximumCalls = maximumCalls;
        RequireSuccess = requireSuccess;
        ArgumentsSubset = argumentsSubset?.Clone();
    }

    /// <summary>Gets the tool alias or canonical identity.</summary>
    public string Tool { get; }

    /// <summary>Gets the least number of matching calls.</summary>
    public int MinimumCalls { get; }

    /// <summary>Gets the greatest number of matching calls, or <see langword="null"/>.</summary>
    public int? MaximumCalls { get; }

    /// <summary>Gets whether matching calls must have terminated successfully.</summary>
    public bool RequireSuccess { get; }

    /// <summary>Gets the arguments a call must contain, or <see langword="null"/>.</summary>
    public JsonElement? ArgumentsSubset { get; }

    /// <inheritdoc/>
    public bool Equals(ExpectedToolCall? other) =>
        other is not null
        && string.Equals(Tool, other.Tool, StringComparison.Ordinal)
        && MinimumCalls == other.MinimumCalls
        && MaximumCalls == other.MaximumCalls
        && RequireSuccess == other.RequireSuccess
        && (ArgumentsSubset is null
            ? other.ArgumentsSubset is null
            : other.ArgumentsSubset is { } otherSubset && JsonElement.DeepEquals(ArgumentsSubset.Value, otherSubset));

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Tool, MinimumCalls, MaximumCalls, RequireSuccess, ArgumentsSubset?.GetRawText());
}
