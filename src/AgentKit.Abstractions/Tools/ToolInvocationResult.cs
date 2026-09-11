// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The terminal outcome of one tool invocation attempt: the same portable
/// <see cref="ToolCallOutcome"/> shape a <see cref="ToolResultPart"/>
/// eventually carries, paired with the result content to return.
/// </summary>
/// <remarks>
/// This is invocation evidence and owned content from the current tool adapter
/// boundary. It is not an authoritative <see cref="ToolCallResult"/> and does
/// not prove normalization, recording, or publication. The tool executor must
/// retain accepted-call correlation, normalize content under its captured policy,
/// record one terminal result, and project that record under the retained
/// projection policy. A publication retry must never repeat the tool effect.
/// </remarks>
public sealed record ToolInvocationResult
{
    /// <summary>Initializes a new instance of the <see cref="ToolInvocationResult"/> record.</summary>
    /// <param name="outcome">The terminal disposition of this invocation.</param>
    /// <param name="content">The ordered result content to return to the model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="content"/> is a default, uninitialized array.
    /// </exception>
    public ToolInvocationResult(ToolCallOutcome outcome, ImmutableArray<ContentPart> content)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfDefault(content);

        Outcome = outcome;
        Content = content;
    }

    /// <summary>Gets the terminal disposition of this invocation.</summary>
    public ToolCallOutcome Outcome { get; init; }

    /// <summary>Gets the ordered result content to return to the model.</summary>
    public ImmutableArray<ContentPart> Content { get; init; }

    /// <inheritdoc/>
    public bool Equals(ToolInvocationResult? other) =>
        other is not null && Outcome.Equals(other.Outcome) && Content.SequenceEqual(other.Content);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Outcome);
        foreach (var part in Content)
        {
            hash.Add(part);
        }

        return hash.ToHashCode();
    }
}
