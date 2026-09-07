// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The tool-call selection policy for one chat model request.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record LlmToolChoice
{
    /// <summary>Gets the shared instance selecting <see cref="LlmToolChoiceMode.Auto"/>.</summary>
    public static LlmToolChoice Auto { get; } = new(LlmToolChoiceMode.Auto, null);

    /// <summary>Gets the shared instance selecting <see cref="LlmToolChoiceMode.None"/>.</summary>
    public static LlmToolChoice None { get; } = new(LlmToolChoiceMode.None, null);

    /// <summary>Gets the shared instance selecting <see cref="LlmToolChoiceMode.Required"/>.</summary>
    public static LlmToolChoice Required { get; } = new(LlmToolChoiceMode.Required, null);

    /// <summary>
    /// Creates a <see cref="LlmToolChoice"/> that forces the model to call
    /// the tool named <paramref name="toolName"/>.
    /// </summary>
    /// <param name="toolName">The name of the tool the model must call.</param>
    /// <returns>A <see cref="LlmToolChoice"/> with <see cref="Mode"/> set to <see cref="LlmToolChoiceMode.Named"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="toolName"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public static LlmToolChoice Named(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return new LlmToolChoice(LlmToolChoiceMode.Named, toolName);
    }

    /// <summary>Initializes a new instance of the <see cref="LlmToolChoice"/> record.</summary>
    /// <param name="mode">The tool-call selection policy.</param>
    /// <param name="forcedToolName">
    /// The name of the tool the model must call when <paramref name="mode"/>
    /// is <see cref="LlmToolChoiceMode.Named"/>; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="mode"/> is <see cref="LlmToolChoiceMode.Named"/> and
    /// <paramref name="forcedToolName"/> is null, empty, or whitespace; or
    /// <paramref name="mode"/> is not <see cref="LlmToolChoiceMode.Named"/>
    /// and <paramref name="forcedToolName"/> is not <see langword="null"/>.
    /// </exception>
    public LlmToolChoice(LlmToolChoiceMode mode, string? forcedToolName)
    {
        if (mode == LlmToolChoiceMode.Named)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(forcedToolName);
        }
        else if (forcedToolName is not null)
        {
            throw new ArgumentException(
                $"{nameof(forcedToolName)} must be null unless {nameof(mode)} is {nameof(LlmToolChoiceMode.Named)}.",
                nameof(forcedToolName));
        }

        Mode = mode;
        ForcedToolName = forcedToolName;
    }

    /// <summary>Gets the tool-call selection policy.</summary>
    public LlmToolChoiceMode Mode { get; init; }

    /// <summary>
    /// Gets the name of the tool the model must call when <see cref="Mode"/>
    /// is <see cref="LlmToolChoiceMode.Named"/>; otherwise
    /// <see langword="null"/>.
    /// </summary>
    public string? ForcedToolName { get; init; }
}
