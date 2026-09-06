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
public sealed record ChatToolChoice
{
    /// <summary>Gets the shared instance selecting <see cref="ChatToolChoiceMode.Auto"/>.</summary>
    public static ChatToolChoice Auto { get; } = new(ChatToolChoiceMode.Auto, null);

    /// <summary>Gets the shared instance selecting <see cref="ChatToolChoiceMode.None"/>.</summary>
    public static ChatToolChoice None { get; } = new(ChatToolChoiceMode.None, null);

    /// <summary>Gets the shared instance selecting <see cref="ChatToolChoiceMode.Required"/>.</summary>
    public static ChatToolChoice Required { get; } = new(ChatToolChoiceMode.Required, null);

    /// <summary>
    /// Creates a <see cref="ChatToolChoice"/> that forces the model to call
    /// the tool named <paramref name="toolName"/>.
    /// </summary>
    /// <param name="toolName">The name of the tool the model must call.</param>
    /// <returns>A <see cref="ChatToolChoice"/> with <see cref="Mode"/> set to <see cref="ChatToolChoiceMode.Named"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="toolName"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public static ChatToolChoice Named(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return new ChatToolChoice(ChatToolChoiceMode.Named, toolName);
    }

    /// <summary>Initializes a new instance of the <see cref="ChatToolChoice"/> record.</summary>
    /// <param name="mode">The tool-call selection policy.</param>
    /// <param name="forcedToolName">
    /// The name of the tool the model must call when <paramref name="mode"/>
    /// is <see cref="ChatToolChoiceMode.Named"/>; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="mode"/> is <see cref="ChatToolChoiceMode.Named"/> and
    /// <paramref name="forcedToolName"/> is null, empty, or whitespace; or
    /// <paramref name="mode"/> is not <see cref="ChatToolChoiceMode.Named"/>
    /// and <paramref name="forcedToolName"/> is not <see langword="null"/>.
    /// </exception>
    public ChatToolChoice(ChatToolChoiceMode mode, string? forcedToolName)
    {
        if (mode == ChatToolChoiceMode.Named)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(forcedToolName);
        }
        else if (forcedToolName is not null)
        {
            throw new ArgumentException(
                $"{nameof(forcedToolName)} must be null unless {nameof(mode)} is {nameof(ChatToolChoiceMode.Named)}.",
                nameof(forcedToolName));
        }

        Mode = mode;
        ForcedToolName = forcedToolName;
    }

    /// <summary>Gets the tool-call selection policy.</summary>
    public ChatToolChoiceMode Mode { get; init; }

    /// <summary>
    /// Gets the name of the tool the model must call when <see cref="Mode"/>
    /// is <see cref="ChatToolChoiceMode.Named"/>; otherwise
    /// <see langword="null"/>.
    /// </summary>
    public string? ForcedToolName { get; init; }
}
