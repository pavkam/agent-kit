// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The terminal outcome of one tool invocation attempt: the same portable
/// <see cref="ToolCallOutcome"/> shape a <see cref="ToolResultPart"/>
/// eventually carries, paired with the result content to return.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Reusing <see cref="ToolCallOutcome"/> here,
/// rather than defining a second parallel outcome hierarchy at the
/// execution-pipeline layer, keeps exactly one portable vocabulary for
/// "what happened to this call" from invocation through to durable
/// history: whatever invoked this tool constructs the eventual
/// <see cref="ToolResultPart"/> directly from this instance's
/// <see cref="Outcome"/> and <see cref="Content"/>.
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
