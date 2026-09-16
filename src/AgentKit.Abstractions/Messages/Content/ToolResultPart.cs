// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A content part carrying the one terminal result for an accepted
/// <see cref="ToolCallPart"/>.
/// </summary>
/// <remarks>
/// A <see cref="ToolResultPart"/> shares its <see cref="CallId"/> with the
/// <see cref="ToolCallPart"/> it answers, which is how the runtime, the
/// provider adapter, and durable history all correlate a request with its
/// outcome without relying on array position. There is exactly one
/// <see cref="ToolResultPart"/> per accepted call; execution failures,
/// rejections, and cancellations are all represented through
/// <see cref="Outcome"/> rather than by omitting the result.
/// </remarks>
public sealed record ToolResultPart: ContentPart
{
    /// <summary>Initializes a new instance of the <see cref="ToolResultPart"/> record.</summary>
    /// <param name="callId">The call identity this result answers.</param>
    /// <param name="tool">The tool that was requested and, when resolution succeeded, invoked.</param>
    /// <param name="outcome">The terminal disposition of the call.</param>
    /// <param name="content">The ordered result content returned to the model.</param>
    /// <param name="projection">The projection provenance for this bounded durable/model-facing view.</param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="tool"/>, <paramref name="outcome"/>, <paramref name="projection"/>, or
    /// <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="content"/> is a default, uninitialized array or contains a null element.
    /// </exception>
    public ToolResultPart(
        ToolCallId callId,
        ToolReference tool,
        ToolCallOutcome outcome,
        ImmutableArray<ContentPart> content,
        ToolResultProjectionInfo projection,
        ExtensionData extensions)
        : base(extensions)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfContainsNull(content);
        ArgumentNullException.ThrowIfNull(projection);

        CallId = callId;
        Tool = tool;
        Outcome = outcome;
        Content = content;
        Projection = projection;
    }

    /// <summary>Gets the call identity this result answers.</summary>
    public ToolCallId CallId { get; init; }

    /// <summary>Gets the tool that was resolved and invoked.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ToolReference Tool
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the terminal disposition of the call.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ToolCallOutcome Outcome
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the ordered result content returned to the model.</summary>
    /// <exception cref="ArgumentException">
    /// The value assigned during initialization or non-destructive mutation is a default,
    /// uninitialized array or contains a null element.
    /// </exception>
    public ImmutableArray<ContentPart> Content
    {
        get;
        init
        {
            ArgumentException.ThrowIfContainsNull(value);
            field = value;
        }
    }

    /// <summary>Gets the projection provenance for this bounded durable/model-facing view.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ToolResultProjectionInfo Projection
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <inheritdoc/>
    public bool Equals(ToolResultPart? other) =>
        other is not null
        && CallId.Equals(other.CallId)
        && Tool.Equals(other.Tool)
        && Outcome.Equals(other.Outcome)
        && Content.SequenceEqual(other.Content)
        && Projection.Equals(other.Projection)
        && Extensions.Equals(other.Extensions);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CallId);
        hash.Add(Tool);
        hash.Add(Outcome);
        foreach (var part in Content)
        {
            hash.Add(part);
        }

        hash.Add(Projection);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
