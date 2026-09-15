// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Binds one model-advertised tool alias to the exact descriptor captured by the same composition.</summary>
/// <remarks>
/// The binding is immutable observational evidence. It neither resolves a current catalog nor grants authority to
/// execute the tool. A conversation validates that <see cref="AdvertisedTool"/> is among its advertised definitions
/// before using <see cref="Descriptor"/> for presentation.
/// </remarks>
public sealed record ConversationToolPresentationBinding
{
    /// <summary>Initializes an exact presentation binding.</summary>
    /// <param name="descriptor">The nonnull descriptor retained from the captured catalog.</param>
    /// <param name="advertisedTool">The nonnull definition, including its provider-visible alias, advertised by the conversation.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">The descriptor and advertised definition identify different tools.</exception>
    public ConversationToolPresentationBinding(ToolDescriptor descriptor, LlmToolDefinition advertisedTool)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(advertisedTool);
        ArgumentException.ThrowIfNotEqual(descriptor.Id, advertisedTool.Id, nameof(advertisedTool));
        Descriptor = descriptor;
        AdvertisedTool = advertisedTool;
    }

    /// <summary>Gets the exact descriptor retained from composition.</summary>
    /// <value>Immutable source, schema, version, and tool identity evidence.</value>
    public ToolDescriptor Descriptor { get; }

    /// <summary>Gets the exact model definition whose name is the advertised alias.</summary>
    /// <value>An immutable definition that must also occur in the conversation's advertised tool list.</value>
    public LlmToolDefinition AdvertisedTool { get; }
}
