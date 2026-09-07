// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The provider-neutral, ready-to-translate content of one chat request:
/// the selected model, ordered messages, available tools, and effective
/// settings.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller
/// <c>ModelRequestContext</c> described by the context-assembly
/// architecture, which additionally carries agent/session/run/turn
/// identity, resolved instructions, security authorization, and a context
/// manifest produced by the not-yet-implemented AgentKit.Context and
/// AgentKit.Session packages. Once those packages exist, a chat model
/// adapter will consume their richer context type instead; this type
/// exists so a chat model implementation has a stable, self-contained
/// request shape to implement and test against in the meantime. It preserves the
/// one piece of identity every downstream consumer already depends on —
/// <see cref="ModelRequestId"/> — so streamed events and the committed
/// response can still be correlated end to end.
/// </para>
/// </remarks>
public sealed record LlmRequestContext
{
    /// <summary>Initializes a new instance of the <see cref="LlmRequestContext"/> record.</summary>
    /// <param name="modelRequestId">
    /// The identity of this request, flowing through every streamed event
    /// and the committed response.
    /// </param>
    /// <param name="model">The selected model descriptor.</param>
    /// <param name="messages">The ordered conversation history to send.</param>
    /// <param name="tools">The tools available for the model to call.</param>
    /// <param name="toolChoice">The tool-call selection policy.</param>
    /// <param name="settings">The effective sampling and output settings.</param>
    /// <param name="extensions">Provider-specific request data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="model"/>, <paramref name="toolChoice"/>,
    /// <paramref name="settings"/>, or <paramref name="extensions"/> is
    /// null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="messages"/> or <paramref name="tools"/> is a default,
    /// uninitialized array.
    /// </exception>
    public LlmRequestContext(
        ModelRequestId modelRequestId,
        ModelDescriptor model,
        ImmutableArray<AgentMessage> messages,
        ImmutableArray<LlmToolDefinition> tools,
        LlmToolChoice toolChoice,
        LlmRequestSettings settings,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfDefault(messages);
        ArgumentException.ThrowIfDefault(tools);
        ArgumentNullException.ThrowIfNull(toolChoice);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(extensions);

        ModelRequestId = modelRequestId;
        Model = model;
        Messages = messages;
        Tools = tools;
        ToolChoice = toolChoice;
        Settings = settings;
        Extensions = extensions;
    }

    /// <summary>
    /// Gets the identity of this request, flowing through every streamed
    /// event and the committed response.
    /// </summary>
    public ModelRequestId ModelRequestId { get; init; }

    /// <summary>Gets the selected model descriptor.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ModelDescriptor Model
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the ordered conversation history to send.</summary>
    /// <exception cref="ArgumentException">
    /// The value assigned during initialization or non-destructive mutation is a default,
    /// uninitialized array.
    /// </exception>
    public ImmutableArray<AgentMessage> Messages
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value);
            field = value;
        }
    }

    /// <summary>Gets the tools available for the model to call.</summary>
    /// <exception cref="ArgumentException">
    /// The value assigned during initialization or non-destructive mutation is a default,
    /// uninitialized array.
    /// </exception>
    public ImmutableArray<LlmToolDefinition> Tools
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value);
            field = value;
        }
    }

    /// <summary>Gets the tool-call selection policy.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public LlmToolChoice ToolChoice
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the effective sampling and output settings.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public LlmRequestSettings Settings
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets provider-specific request data.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ExtensionData Extensions
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <inheritdoc/>
    public bool Equals(LlmRequestContext? other) =>
        other is not null
        && ModelRequestId.Equals(other.ModelRequestId)
        && Model.Equals(other.Model)
        && Messages.SequenceEqual(other.Messages)
        && Tools.SequenceEqual(other.Tools)
        && ToolChoice.Equals(other.ToolChoice)
        && Settings.Equals(other.Settings)
        && Extensions.Equals(other.Extensions);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ModelRequestId);
        hash.Add(Model);
        foreach (var message in Messages)
        {
            hash.Add(message);
        }

        foreach (var tool in Tools)
        {
            hash.Add(tool);
        }

        hash.Add(ToolChoice);
        hash.Add(Settings);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
